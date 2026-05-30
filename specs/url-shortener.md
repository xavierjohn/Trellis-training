# URL Shortener — Specification

> This specification describes an HTTP API service that issues short codes for long URLs and redirects browsers from the short form back to the original. It is intended to be given to an AI along with the Trellis Copilot Instructions to generate a working .NET application. The spec focuses on business requirements and outcomes. Implementation patterns come from the Copilot Instructions.
>
> **This lab is deliberately unversioned.** The host must not register `AddApiVersioning(...)`, must not declare `ApiVersion` attributes, and must not call `WithVersionedRoute(...)`. All routes are plain (`/links`, `/links/{id}`, `/{shortCode}`) with no `?api-version=` query parameter. The spec exists to stress Trellis HTTP primitives — `WithLocation`, `HttpContext.PageUrl`, `AddTrellisProblemDetails`, paginated GET ergonomics, ETag / `If-None-Match`, `IActorProvider`, `Result<T>` — in a host that never opted into versioning. Implementations should compose framework primitives without reaching for the versioning helpers (which would either be a no-op or a runtime error in an unversioned host).

## 1. Domain Overview

A team runs an internal URL-shortener service. Authenticated users mint short codes for long URLs they want to share. Anyone with the short URL — including unauthenticated visitors — can follow it and get redirected to the original. Owners can see click counts on their own links and disable or delete them. An "admin" permission allows operators to manage any link.

The service is HTTP-shaped end-to-end: every operation is a request handler. There is no background job, no scheduled work, no event consumer. What makes the lab non-trivial is the mix of shapes in one host: a permission-gated CRUD surface alongside an anonymous redirect, ETag-cacheable stats alongside non-cacheable writes, and idempotent POST under the `Idempotency-Key` header pattern. All of it without API versioning.

## 2. Ubiquitous Language

| Term | Definition |
|------|------------|
| **Link** | A persistent mapping from a `ShortCode` to an `OriginalUrl`, owned by exactly one user. May be disabled or expired. |
| **Short Code** | The opaque path segment used in the short URL. Either user-chosen (custom) or system-generated. Globally unique across all links. |
| **Original URL** | The destination the redirect resolves to. Absolute http/https URL, ≤2048 chars. |
| **Owner** | The actor that created the link. Owner is captured at creation and never changes. |
| **Click** | One recorded visit to `GET /{shortCode}`. Append-only. Carries clicked-at timestamp and optional user-agent / referer-host metadata. |
| **Link Stats** | A read projection over a link's clicks: `TotalClicks`, `FirstClickAt: Maybe<...>`, `LastClickAt: Maybe<...>`. Derived on read; not stored as a separate aggregate. |
| **Expired Link** | A link whose `ExpiresAt` (if set) is in the past. Expired links behave as missing for redirect (`410 Gone`) but remain visible to the owner and to admins. |
| **Disabled Link** | A link whose `IsActive` flag has been set to false by the owner or an admin. Disabled links behave as missing for redirect (`410 Gone`) but remain visible to the owner and to admins. |
| **Idempotency Key** | An opaque client-supplied string (header `Idempotency-Key`) that lets a client retry `POST /links` safely. The pair `(OwnerId, IdempotencyKey)` is unique. Repeating the same key returns the original link (200 OK, not 201 Created) without creating a duplicate. |
| **Anonymous Visitor** | The actor on `GET /{shortCode}` and `GET /health`. Both endpoints accept requests with no bearer token; the framework must produce `Maybe<Actor>.None` for these requests, not a fall-back system identity. |

## 3. Aggregates

### 3.1 Link Aggregate

**Identity:** `LinkId` (unique identifier).

**Properties:**
- `ShortCode` — required, unique system-wide, 4–12 chars, `[A-Za-z0-9_-]+`
- `OriginalUrl` — required, `OriginalUrl` value object (absolute http/https, ≤2048 chars)
- `OwnerId` — required `OwnerId` value object; captured at creation, immutable
- `CreatedAt` — required UTC timestamp
- `ExpiresAt` — `Maybe<DateTimeOffset>`; when `Some`, must be strictly after `CreatedAt`
- `IsActive` — boolean; defaults to `true`

**Rules:**
- A link cannot be created with `ExpiresAt <= CreatedAt`.
- A link cannot be created with an `OriginalUrl` whose scheme is neither `http` nor `https`.
- A link cannot be created with a `ShortCode` that already exists (storage-layer unique constraint; see §10).
- `OwnerId` is immutable after creation.
- `ShortCode` is immutable after creation. (Renaming would break every short link already in circulation.)
- `OriginalUrl` is immutable after creation. (Re-pointing an existing short link to a different destination is out of scope; users mint a new link instead — see §13.)

**Operations:**
- `Disable()` — sets `IsActive` to false. Idempotent. Raises `LinkDisabledDomainEvent` only on the first call (the call that actually changes state).
- `Enable()` — sets `IsActive` to true. Idempotent. Raises `LinkEnabledDomainEvent` only on the first call.
- `Extend(newExpiresAt)` — sets `ExpiresAt`. The new value must be strictly later than the current `ExpiresAt` (if any) and strictly after `now`. Returns `Result.Fail(Error.InvalidInput)` otherwise. Raises `LinkExpiryExtendedDomainEvent`.
- `Effective state at time t`: a link is **eligible for redirect** if `IsActive == true && (ExpiresAt is None || ExpiresAt > t)`. Eligibility is computed, not stored.

### 3.2 Click Aggregate

**Identity:** `ClickId` (unique identifier).

**Properties:**
- `LinkId` — reference to the link this click targets
- `ClickedAt` — required UTC timestamp
- `UserAgent` — `Maybe<UserAgent>` (opaque string, ≤500 chars); populated from the inbound `User-Agent` header when present and non-empty
- `RefererHost` — `Maybe<RefererHost>` (DNS hostname, ≤253 chars); derived from the inbound `Referer` header by extracting its host component. When absent, malformed, or longer than 253 chars, store as `None` rather than failing the redirect.

**Rules:**
- Clicks are append-only. There is no `Click` mutation operation.
- Failing to record a click must **not** fail the redirect. The user's browser must always receive the 302 (or 410) the link warrants, even if click persistence throws. The orchestrator catches and logs at Warning; the response status is unchanged.

**Operations:** none beyond construction.

### 3.3 IdempotencyRecord (Auxiliary Aggregate)

**Identity:** composite `(OwnerId, IdempotencyKey)`.

**Properties:**
- `OwnerId` — required
- `IdempotencyKey` — required `IdempotencyKey` value object (1–64 chars, opaque)
- `LinkId` — reference to the link the original `POST /links` produced
- `CreatedAt` — required UTC timestamp

**Rules:**
- `(OwnerId, IdempotencyKey)` is unique system-wide.
- An idempotency record is created **in the same database transaction** as its corresponding `Link` row. A failure to commit one rolls back the other.

**Operations:** none beyond construction. Records are not mutated. They may be pruned by an out-of-band job after 24 hours; pruning is not in scope for this lab (declared in §13).

## 4. State Machine

The `Link` state machine is intentionally trivial — the lab's interesting state is the redirect-eligibility derivation, not a workflow.

```
                ┌────────────┐
                │  Active    │ ◄─── (initial, IsActive = true)
                └────────────┘
                   │     ▲
            Disable│     │ Enable
                   ▼     │
                ┌────────────┐
                │  Disabled  │
                └────────────┘
```

`Expired` is **not** a stored state. It is a derived property: `ExpiresAt is Some(t) && t <= now`. A link can be active-and-expired (recently expired, owner hasn't touched it) or disabled-and-expired (owner disabled it before expiry kicked in). Both render as `410 Gone` for the redirect endpoint and as visible records for the owner.

**Transitions:**

| From | To | Trigger | Side effect |
|------|----|---------|-------------|
| `Active` | `Disabled` | `Disable()` | Sets `IsActive = false`. Raises `LinkDisabledDomainEvent` (only when state actually changes). |
| `Disabled` | `Active` | `Enable()` | Sets `IsActive = true`. Raises `LinkEnabledDomainEvent` (only when state actually changes). |
| any | any (no transition) | `Disable()` on already-disabled, `Enable()` on already-active | No-op success. No event. |

**Invalid transitions:** none on the state graph itself (the graph has only two states and both transitions are valid in their starting state). Invalid *inputs* (e.g., `Extend(expiresAt)` where the new value is not later than the current one) return `Result.Fail(Error.InvalidInput)` without state change.

## 5. HTTP Surface

This service exposes the following endpoints. **There is no `?api-version=` query parameter on any route.** The host must not register `AddApiVersioning(...)`, must not decorate handlers with `ApiVersion` attributes, and must not call `WithVersionedRoute(...)`. Plain `MapGet` / `MapPost` (or attribute-routed `[HttpGet("links")]`) is correct.

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| POST   | `/links` | `links:create` | Create a new short link. Optional `Idempotency-Key` header. |
| GET    | `/links` | `links:read` | List the calling actor's links (paginated). `links:admin` sees all. |
| GET    | `/links/{id}` | `links:read` (owner) or `links:admin` | Get one link by id. |
| PUT    | `/links/{id}` | `links:write` (owner) or `links:admin` | Toggle `IsActive` and/or extend `ExpiresAt`. |
| DELETE | `/links/{id}` | `links:write` (owner) or `links:admin` | Delete a link. Cascades to clicks (see §10). |
| GET    | `/links/{id}/stats` | `links:read` (owner) or `links:admin` | Click stats projection. Supports `If-None-Match` → 304. |
| GET    | `/{shortCode}` | anonymous | 302 redirect to the original URL. 410 if disabled/expired. 404 if unknown. |
| GET    | `/health` | anonymous | Liveness probe. |

**Route precedence note.** `/{shortCode}` is a wildcard at the root. The service routes must be ordered so that `/links`, `/links/{id}`, `/links/{id}/stats`, and `/health` win over `/{shortCode}`. The standard ASP.NET Core minimal-API / attribute-routing precedence rules already give literal paths and longer templates priority over single-segment wildcards; the lab does not require any special order declaration, but implementations must not introduce a custom matcher that breaks that default.

### 5.1 Request and Response Shapes

**`POST /links`**

Request:

```json
{
  "originalUrl": "https://example.com/very/long/path?with=query",
  "customShortCode": "promo-2026",
  "expiresAt": "2026-12-31T23:59:59Z"
}
```

- `originalUrl` — required.
- `customShortCode` — optional. When omitted, the service generates a code.
- `expiresAt` — optional. When omitted, the link does not expire.

Optional header: `Idempotency-Key: <opaque, 1-64 chars>`.

Response (new link): `201 Created`, `Location: /links/{id}`, body = `LinkView`.

Response (idempotent replay, same key as a prior request from the same owner): `200 OK`, `Location: /links/{id}`, body = the existing `LinkView`. The body must match the original creation response byte-for-byte (modulo timestamps that are read at response-build time — there should not be any of those on `LinkView`).

`LinkView`:

```json
{
  "id": "...",
  "shortCode": "promo-2026",
  "shortUrl": "https://<host>/promo-2026",
  "originalUrl": "https://example.com/very/long/path?with=query",
  "ownerId": "...",
  "createdAt": "2026-05-30T14:00:00Z",
  "expiresAt": "2026-12-31T23:59:59Z",
  "isActive": true
}
```

`expiresAt` is omitted (not `null`) when the link does not expire. (The framework's standard `Maybe<T>` JSON behaviour applies.)

**`GET /links?page=<n>&pageSize=<m>`**

Response: `200 OK`, body =

```json
{
  "items": [ /* LinkView, LinkView, ... */ ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 137
}
```

`Link` header: `<...?page=2&pageSize=50>; rel="next"`, `<...?page=1&pageSize=50>; rel="prev"` (built via `HttpContext.PageUrl(...)`). `prev` is absent on page 1; `next` is absent on the last page.

`pageSize` defaults to 50, maximum 100. Values outside `[1, 100]` produce `400 Bad Request` via ProblemDetails.

**`GET /links/{id}/stats`**

Response: `200 OK` with strong `ETag`. Body =

```json
{
  "linkId": "...",
  "totalClicks": 42,
  "firstClickAt": "2026-05-30T14:01:00Z",
  "lastClickAt": "2026-05-30T15:22:33Z"
}
```

`firstClickAt` and `lastClickAt` are omitted when `totalClicks == 0`.

The `ETag` is derived deterministically from the projection state: the lab requires `ETag: "{totalClicks}-{lastClickTicks}"` (where `lastClickTicks` is `LastClickAt?.UtcTicks` or `0` when `None`). A client sending `If-None-Match` with that exact value receives `304 Not Modified` and an empty body. Any other value (or absence of the header) yields `200 OK` with the body and the current `ETag`.

`Cache-Control: private, max-age=60` on the `200` response. Omit on the `304` response.

**`GET /{shortCode}`**

- Link not found → `404 Not Found` (ProblemDetails body).
- Link found but `!IsActive` or expired → `410 Gone` (ProblemDetails body, `title` = "Link is no longer active").
- Link found and eligible → `302 Found` with `Location: <originalUrl>` and `Cache-Control: no-store`. Body empty.

A click row must be persisted **before** the response is written when the outcome is `302`. If click persistence fails, the redirect still succeeds (see §3.2). Clicks are not recorded for `404` or `410`.

**`GET /health`**

`200 OK` with `{"status": "healthy"}`. No counters, no last-tick state — there is no tick. Always 200 if the process is responsive enough to handle a request.

## 6. Operations (Use Cases)

All operations are implemented as Commands or Queries using CQRS, executed through the mediator pipeline. Every handler returns `Result<T>` (or `Result` for void). The error categorisation (§9) determines the HTTP status the response writer produces.

### 6.1 Create Link (Command)

- **Permission required:** `links:create`.
- **Input:** `OwnerId`, `OriginalUrl`, `customShortCode: Maybe<ShortCode>`, `expiresAt: Maybe<DateTimeOffset>`, `idempotencyKey: Maybe<IdempotencyKey>`.
- **Behaviour:**
  1. If `idempotencyKey` is `Some(k)`:
     - Open a database transaction.
     - INSERT into `IdempotencyRecords` with `(OwnerId, k, newLinkId, now)`. If the insert succeeds, proceed to step 2 within the same transaction. If the insert raises a unique-constraint violation on `(OwnerId, IdempotencyKey)`, roll back, SELECT the existing record, load the referenced `Link`, and return `Result.Ok(CreateLinkOutcome.Replayed(link))`.
  2. Resolve the short code:
     - If `customShortCode` is `Some(s)`, use `s`.
     - Else, generate a new code (system-generated codes are 8 chars, `[A-Za-z0-9]`, drawn from a CSPRNG).
  3. INSERT the new `Link`. If a unique-constraint violation on `ShortCode` is raised:
     - If `customShortCode` was supplied: return `Result.Fail(Error.Conflict(...))`. (The user must pick a different code.)
     - If the code was system-generated: regenerate and retry, up to 5 attempts. On exhaustion, return `Result.Fail(Error.Unavailable("short-code-generation-exhausted"))`.
  4. Commit the transaction.
  5. Return `Result.Ok(CreateLinkOutcome.Created(link))`.
- **Output:** `Result<CreateLinkOutcome>` where `CreateLinkOutcome` is one of `Created(link) | Replayed(link)`. The HTTP layer maps `Created` to `201` and `Replayed` to `200`.

### 6.2 List My Links (Query)

- **Permission required:** `links:read`.
- **Input:** `actor` (resolved by the mediator pipeline from `IActorProvider`), `page`, `pageSize`.
- **Behaviour:** returns the actor's links, ordered by `CreatedAt DESC`. If the actor has `links:admin`, returns all links; otherwise filters by `OwnerId == actor.Id`.
- **Output:** `Result<Page<LinkView>>` carrying items + `TotalCount`. The HTTP layer wraps with `HttpContext.PageUrl(...)` for `Link` headers.

### 6.3 Get Link By Id (Query)

- **Permission required:** `links:read`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:**
  - Load by id. If not found, return `Result.Fail(Error.NotFound)`.
  - If `link.OwnerId != actor.Id` and the actor does not have `links:admin`, return `Result.Fail(Error.NotFound)` — not `Error.Forbidden`. (Surfacing "this link exists but you can't see it" leaks existence.)
  - Otherwise return `Result.Ok(LinkView)`.

### 6.4 Update Link (Command)

- **Permission required:** `links:write`.
- **Input:** `LinkId`, `actor`, optional `isActive: Maybe<bool>`, optional `expiresAt: Maybe<DateTimeOffset>`.
- **Behaviour:**
  - Load. Apply ownership rule per §6.3 (return `Error.NotFound` if not owner and not admin).
  - If `isActive` is `Some(true)` and current state is `Disabled` → `Enable()`. If `Some(false)` and current state is `Active` → `Disable()`. If `Some(x)` and current state already matches → no-op success.
  - If `expiresAt` is `Some(t)` → `Extend(t)`. May fail `Error.InvalidInput` if `t` is not strictly later than current `ExpiresAt` (or not strictly later than `now`).
  - Persist any state change. Return `Result.Ok(LinkView)` with the post-update state.

### 6.5 Delete Link (Command)

- **Permission required:** `links:write`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:** Load + ownership-check per §6.3. Delete the link and all associated clicks (see §10 for cascade configuration). Return `Result.Ok`.

### 6.6 Get Link Stats (Query)

- **Permission required:** `links:read`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:**
  - Ownership-check per §6.3.
  - Compute `LinkStats` from the `Clicks` table: `TotalClicks = COUNT(*)`, `FirstClickAt = MIN(ClickedAt)`, `LastClickAt = MAX(ClickedAt)`. A single aggregate-projection query, not a load-all-clicks-then-aggregate.
  - Return `Result.Ok(LinkStatsView)`.
- **HTTP layer:** computes ETag deterministically from `(TotalClicks, LastClickTicks)`. If the request's `If-None-Match` matches, respond `304 Not Modified` and skip body serialisation entirely.

### 6.7 Redirect (Query) + Record Click (Command)

- **Permission required:** none (anonymous).
- **Input:** `ShortCode`, optional `UserAgent`, optional `Referer`.
- **Behaviour:**
  - Lookup by `ShortCode`. Not found → `Result.Fail(Error.NotFound)`. HTTP layer maps to `404`.
  - Eligible (active, not expired) → emit `RedirectOutcome.Redirect(originalUrl)`. HTTP layer maps to `302` with `Location` header.
  - Disabled or expired → emit `RedirectOutcome.Gone`. HTTP layer maps to `410 Gone`.
  - On `Redirect` (only): record a click via `RecordClickCommand`. Click recording errors must be caught and logged at Warning; they do not change the response.
- **`RecordClickCommand`** is a small handler taking `LinkId`, `now`, `UserAgent?`, `Referer?` and appending one `Click` row. Permission is none (it is internal to the redirect orchestrator and never reached from an HTTP route directly).

### 6.8 Health (Query)

Anonymous. Returns `{ "status": "healthy" }`. Always `200`. There is no underlying check beyond "the process can handle a request" — Trellis `IHealthCheck` integration is out of scope for this lab.

## 7. Idempotency

Idempotency applies only to `POST /links`. All other writes (`PUT /links/{id}`, `DELETE /links/{id}`) are naturally idempotent at the resource level: repeating them with the same body produces the same state.

The contract for `POST /links`:

- **No `Idempotency-Key` header:** create a new link on every call. Two POSTs with the same body produce two links with different short codes (unless `customShortCode` is supplied — then the second call returns `Error.Conflict`).
- **`Idempotency-Key` header present:**
  - On first observation of `(OwnerId, key)`: create the link and the idempotency record in one transaction. Respond `201 Created`.
  - On any subsequent observation of the same `(OwnerId, key)`: do not create anything new. Look up the original link via the record, respond `200 OK` with that link's `LinkView` and `Location` header.
  - The response body of the replay must be byte-equivalent to the original `201` body. The lab does not snapshot the original response; equivalence is achieved by serialising the same `LinkView` from the same `Link` row. (`LinkView` carries no fields that change at read time.)

**Why the record is per-owner, not global.** Idempotency keys are opaque client-supplied strings. Two unrelated clients may legitimately use the same key (e.g., a UUID generator that wraps, or a fixed retry-token). Scoping to `(OwnerId, key)` prevents one client's retries from masking another client's creates.

**Storage-layer contract.** The `(OwnerId, IdempotencyKey)` uniqueness must be enforced by a database constraint, not by a read-then-decide check in the handler. A read-then-decide check races across concurrent retries from the same client.

## 8. Pagination

The list endpoint (`GET /links`) supports `?page=<n>&pageSize=<m>` where:

- `page` defaults to 1, minimum 1.
- `pageSize` defaults to 50, maximum 100. Values outside `[1, 100]` produce `400 Bad Request` via ProblemDetails (a framework-level validation failure, surfaced as `Error.InvalidInput.ForField("pageSize", ...)`).
- The response body is `{ items, page, pageSize, totalCount }`.
- `Link` headers (`rel="next"`, `rel="prev"`, `rel="first"`, `rel="last"`) are constructed via `HttpContext.PageUrl(...)`. The lab requires use of this helper, not hand-built URLs. This is the central test of whether `PageUrl` composes cleanly in an unversioned host (it must not inject `api-version` and must not throw on a target with no `ApiVersionMetadata`).
- The "first" link references `page=1`. The "last" link references `ceil(totalCount / pageSize)`. The "prev" and "next" links are absent on the first and last pages respectively.

## 9. Authorization

**Permissions (claim type: `permission`):**
- `links:create` — required to POST a link.
- `links:read` — required to GET own links and own stats.
- `links:write` — required to PUT and DELETE own links.
- `links:admin` — bypasses ownership filtering; sees and manages all links.

**Actor resolution.** The host registers Trellis's HTTP `IActorProvider` (the standard `DevelopmentActorProvider` for the lab; a real IdP-backed provider in production). There is exactly one `IActorProvider` registration. The lab does not require a worker-style composition (see the worker spec for that pattern) because every operation in this service runs in an HTTP request scope.

**Anonymous endpoints (`/{shortCode}` and `/health`):**
- The HTTP framework must accept requests with no bearer token without 401-ing them at the auth layer (i.e., these endpoints declare anonymous access).
- For these endpoints, `IActorProvider.GetCurrentActorAsync()` returning `Maybe<Actor>.None` is permitted and expected. The handler does not require a permission claim.
- An authenticated request that hits `/{shortCode}` is treated identically to an anonymous one. The redirect is independent of identity.

**Ownership-versus-existence (§6.3 rule).** When an actor without `links:admin` requests a link they do not own, the response is `404 Not Found`, not `403 Forbidden`. Surfacing 403 leaks the existence of a link with that id. The handler returns `Result.Fail(Error.NotFound)` in both cases. This is a deliberate framework test: the spec asserts that the Trellis pattern of returning `Error.NotFound` from the handler (rather than `Error.Forbidden`) produces the right HTTP status without any special-case wiring.

## 10. Persistence

- **Database:** SQLite (file-based, zero setup).
- **Connection string** in `appsettings.Development.json`: `Data Source=UrlShortener.db`.
- **Entities to persist:** `Link`, `Click`, `IdempotencyRecord`.
- **Unique constraints:**
  - `Links(ShortCode)` — unique. Enforces global short-code uniqueness.
  - `IdempotencyRecords(OwnerId, IdempotencyKey)` — composite unique.
- **Indexes:**
  - `Links(OwnerId, CreatedAt DESC)` — supports the owner-scoped list query.
  - `Clicks(LinkId, ClickedAt)` — supports stats aggregation.
- **Cascade:** deleting a `Link` row cascades to its `Click` rows. Configured via EF Core `OnDelete(DeleteBehavior.Cascade)` on the `Link → Clicks` relationship.
- **Enums** (none in this domain — `IsActive` is bool).
- **`Maybe<T>` columns** (`ExpiresAt`, `UserAgent`, `RefererHost`) stored as nullable columns, round-tripping to `Maybe.None` when null.
- **Database creation:** Use `EnsureCreated()` on startup in development mode. Do NOT use EF Core migrations.

**Click persistence in the redirect path.** The `Click` insert must be on a fresh DI scope from the request scope (or use a no-tracking write context if you prefer) so that an EF concurrency exception in click persistence does not poison the request scope and disrupt subsequent middleware. Implementation choice is open; the binding contract is "click failure does not change the redirect status code" (§3.2).

## 11. Caching

`GET /links/{id}/stats` is the only cached endpoint.

- `ETag: "{totalClicks}-{lastClickTicks}"` (strong validator). `lastClickTicks = LastClickAt?.UtcTicks ?? 0`.
- `Cache-Control: private, max-age=60` on the `200` response.
- `If-None-Match: "..."` matching the current ETag → `304 Not Modified`, no body, no `Cache-Control` header (the cached entry is still valid; the proxy/browser already has the headers).
- `If-None-Match: "*"` matches any current ETag and behaves the same way.
- The handler must short-circuit before serialising the body on a 304. The lab does not require any specific framework helper for ETag (Trellis's `WithCacheControl` and ETag helpers are encouraged but not mandated); the binding contract is the externally observable behaviour.

The cache validator is **not** a strong concurrency guard for writes. There is no `If-Match` round-trip on stats (stats is read-only). The validator exists to save bandwidth on polling clients.

## 12. Error Behavior

The mediator pipeline maps each `Error` type to an HTTP response via `AddTrellisProblemDetails` + `UseTrellisProblemDetails`. The full table:

| Situation | `Error` returned by handler | HTTP status | ProblemDetails type |
|-----------|------------------------------|-------------|---------------------|
| Anonymous request to a `links:*` endpoint | (none — auth middleware short-circuits before the handler runs) | `401 Unauthorized` | `Error.AuthenticationRequired` |
| Authenticated request missing the required permission | `Error.Forbidden(policyId, resource?)` from the auth filter | `403 Forbidden` | `Error.Forbidden` |
| `POST /links` body invalid (`originalUrl` missing, scheme not http/https, `customShortCode` regex fail) | `Error.InvalidInput.ForField(...)` | `422 Unprocessable Entity` | `Error.InvalidInput` |
| `POST /links` with `customShortCode` that already exists | `Error.Conflict(reasonCode?, resource?)` | `409 Conflict` | `Error.Conflict` |
| `POST /links` with `Idempotency-Key` whose stored body does not match the current request | `Error.Conflict("idempotency-key-mismatch")` *(see note below)* | `409 Conflict` | `Error.Conflict` |
| System-generated short-code collisions exhausted retries | `Error.Unavailable("short-code-generation-exhausted")` | `503 Service Unavailable` | `Error.Unavailable` |
| `GET /links?pageSize=200` (out of range) | `Error.InvalidInput.ForField("pageSize", ...)` | `400 Bad Request` (framework-level validation) | `Error.InvalidInput` |
| `GET /links/{id}` where id doesn't exist OR is owned by another non-admin actor | `Error.NotFound` | `404 Not Found` | `Error.NotFound` |
| `GET /{shortCode}` where short-code doesn't exist | `Error.NotFound` | `404 Not Found` | `Error.NotFound` |
| `GET /{shortCode}` where link is disabled or expired | `Error.Gone(reasonCode)` *(if Trellis has a `Gone` error; else `Error.InvariantViolation("link-no-longer-active")` mapped explicitly to 410)* | `410 Gone` | `Error.Gone` or fallback |
| `PUT /links/{id}` with invalid `expiresAt` (not strictly later than current) | `Error.InvalidInput.ForField("expiresAt", ...)` | `422 Unprocessable Entity` | `Error.InvalidInput` |

**On "idempotency-key-mismatch".** The strictest behaviour from RFC draft `idempotency-header-01` is: if the same key is reused with a *different* request body, reject with `409 Conflict`. The lab requires this strict behaviour. Equivalence is judged on the canonical-JSON form of the three input fields (`originalUrl`, `customShortCode`, `expiresAt`); identity comparison on `OwnerId` is implicit (records are scoped per owner). Storing the original request body is the simplest implementation.

**On `Error.Gone`.** Trellis may or may not ship a dedicated `Error.Gone` type. If it does, use it. If it does not, the lab accepts either (a) using `Error.NotFound` (treating disabled/expired as effectively absent) and not distinguishing 404 from 410 — this is a scoring deduction in the rubric for response-shape conformance, but acceptable for build correctness — or (b) hand-mapping a custom error type to status 410 in the response writer. Option (b) is preferred. The discovery of "the framework does not have a `Gone` type" is itself a `TRELLIS_FEEDBACK.md` entry.

## 13. Testing Requirements

> **Coverage bar:** the prose below summarises the test categories. The full per-row coverage matrix lives in [`coverage-checklist-url-shortener.md`](./coverage-checklist-url-shortener.md) and is the binding stop-criterion: every row in §1–§9 of that checklist must have at least one matching assertion.

### 13.1 Domain Tests

Unit tests for value objects and aggregate rules. No external dependencies.

- Value objects: `ShortCode`, `OriginalUrl`, `IdempotencyKey`, `UserAgent`, `RefererHost`, every identity type. Each tests `TryCreate` happy path, boundary conditions, null/empty, format violations, equality.
- `Link`: valid creation, `ExpiresAt > CreatedAt` rule, `OriginalUrl` scheme rule, `Disable`/`Enable` idempotency (no event on no-op call), `Extend` rule (must be strictly later than current and strictly after now), immutability of `OwnerId`/`ShortCode`/`OriginalUrl` (no setters or only-internal setters).
- `Click`: construction; `UserAgent` and `RefererHost` accept `Maybe<None>` and `Maybe<Some>`.

### 13.2 Application Tests

Handler tests with fake repositories.

- `CreateLinkCommand`: happy path with auto-generated code; happy path with custom code; conflict on custom code already taken; auto-code regeneration on collision (with a deterministic seed for the test); idempotency-key first observation; idempotency-key replay (returns same `LinkId`, raises no new event); idempotency-key reuse with different body → `Error.Conflict`.
- `ListMyLinksQuery`: owner-scoped filter; admin sees all; pagination `page`/`pageSize` honoured; ordering by `CreatedAt DESC`.
- `GetLinkByIdQuery`: own link returns 200; another's link returns `Error.NotFound` (not `Error.Forbidden`); admin sees another's link.
- `UpdateLinkCommand`: toggle active/disabled; extend expiry; reject invalid expiry; ownership check enforces `Error.NotFound` for non-owner non-admin.
- `DeleteLinkCommand`: deletes own link; cascades to clicks (verified at the persistence layer in §13.4); non-owner gets `Error.NotFound`.
- `GetLinkStatsQuery`: zero-click projection (`firstClickAt`/`lastClickAt` are `None`); non-zero projection; permission/ownership behaviour.
- `RedirectQuery`: 302 outcome for eligible link; `Gone` outcome for disabled link; `Gone` outcome for expired link; `NotFound` for unknown short code.
- `RecordClickCommand`: appends a row; failure does not bubble (handler swallows and logs); recorded fields match input.
- Authorisation: every command/query succeeds with an actor holding the required permission; fails with an actor lacking it. The redirect endpoint and health endpoint succeed with no actor at all.

### 13.3 HTTP Integration Tests

Use `WebApplicationFactory` as normal.

- **The unversioned-host contract** (the lab's central test):
  - All routes are reachable **without** an `?api-version=` query parameter.
  - Adding an `?api-version=1.0` query parameter to any route is either a no-op (accepted and ignored) or produces a framework-defined 400 — the lab does not require a specific outcome, but documents whichever the implementation chose.
  - The host's `Program.cs` (or DI composition) does **not** call `AddApiVersioning(...)`.
  - No handler signature includes an `ApiVersion` parameter.
- `POST /links`: 201 Created with `Location` header pointing to `/links/{id}` (no `?api-version=` in the header value); 422 for bad body; 409 for `customShortCode` collision; idempotency-key replay returns 200 with the same body bytes as the original 201; idempotency-key mismatch returns 409.
  - **The `WithLocation` test:** assert that the `Location` header value is `/links/{id}` with no query parameters appended. This is the most direct check that `WithLocation` did not inject an api-version on a target with no `ApiVersionMetadata`.
- `GET /links`: 200 with body + `Link` header. The `Link` header values must not contain `api-version=`. Pagination `next`/`prev`/`first`/`last` round-trip via `HttpContext.PageUrl(...)`.
  - **The `HttpContext.PageUrl` test:** assert the `Link` header on the response is well-formed and contains no `api-version=` query parameter. This is the central alpha.305 regression test against the lab.
- `GET /links/{id}`: own link 200; another's link 404 (not 403); admin sees another's 200.
- `PUT /links/{id}`: state changes round-trip; invalid expiry returns 422.
- `DELETE /links/{id}`: 204; subsequent GET returns 404; clicks gone (§13.4).
- `GET /links/{id}/stats`: 200 with ETag header. Second request with `If-None-Match: <same etag>` → 304 with empty body. Mutating state (a click in between) changes the ETag.
- `GET /{shortCode}`: 302 with `Location: <originalUrl>` for active link; 410 for disabled; 410 for expired; 404 for unknown. Anonymous access — no `Authorization` header on the request.
  - A click row is persisted on 302 (verified by reading the `Clicks` table after the response).
  - A click row is **not** persisted on 410 or 404.
- `GET /health`: 200 with anonymous access.
- ProblemDetails wrapping: error responses follow RFC 7807 via `AddTrellisProblemDetails`. `type`, `title`, `status`, `detail`, `instance` are all present and correct.

### 13.4 Persistence Tests

Round-trip tests against real SQLite (file or shared in-memory connection — not the in-memory EF Core provider, so unique-constraint behaviour is exercised).

- `Link` insert + reload: every property (`ExpiresAt` absent + present, `IsActive`, `OwnerId`, `OriginalUrl`, `ShortCode`) survives a save + reload.
- `Click` insert + reload: `UserAgent` absent + present, `RefererHost` absent + present, `ClickedAt`.
- `IdempotencyRecord` insert + reload: `(OwnerId, IdempotencyKey)` round-trips; `LinkId` references valid `Link`.
- Unique constraint: `Links(ShortCode)` rejects a duplicate.
- Unique constraint: `IdempotencyRecords(OwnerId, IdempotencyKey)` rejects a duplicate (same owner, same key); accepts the same key from a different owner.
- Cascade: deleting a `Link` deletes its `Click` rows.
- Index: `Links(OwnerId, CreatedAt DESC)` is declared (verified via `Model` inspection or schema dump).
- `DueLinkSpecification`-equivalent (if implemented as a specification): translation via `Where(spec)` (SQLite integration test) and matches in-memory evaluation. *(Optional — the spec does not require a `Specification<T>` for the list query; it is a simple `Where(OwnerId == ...).OrderBy(CreatedAt)`. If the implementation introduces a specification anyway, it must round-trip.)*

### 13.5 Observability Tests

Lightweight relative to the worker lab — there is no tick to emit per-cycle counters for.

- Standard ASP.NET request logging is in place; one request log entry per HTTP request.
- A counter `links.created` (or a similarly-named instrument) increments on each successful `POST /links`. Hand-rolled `Meter` registration is fine; the lab does not mandate Trellis-specific helpers for metrics.
- A counter `redirects.served` increments on each `302` from the redirect endpoint, tagged with the outcome (`redirect`, `gone`, `not-found` — even the not-found case is metric-worthy because it tells operators which short codes are being probed).

Observability beyond this is encouraged but not required for the lab to score "complete".

## 14. Out of Scope

The following are explicitly excluded from this lab. They are mentioned to prevent scope creep:

- Custom domains (every link uses the host the service is deployed on).
- QR-code generation.
- Geographic, device, or browser-family analytics on clicks (the lab records `UserAgent` and `RefererHost` as opaque strings; it does not parse them).
- Click fraud detection or per-link rate limiting.
- Bulk link import / export.
- Link previews (fetching the original URL to extract title / og:image).
- A web UI. The service is API-only.
- Multi-tenancy beyond per-owner scoping (no concept of "organisation" or "team").
- API versioning. (The whole point of this lab is to exercise the unversioned-host path.)
- Background pruning of `IdempotencyRecords`. The spec says 24-hour TTL; implementing the pruner is out of scope. A real deployment would add it.
- Re-pointing an existing short link to a different URL. (Users mint a new link with a new short code instead.)
