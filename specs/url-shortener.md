# URL Shortener — Specification

> This specification describes an HTTP service that issues short codes and redirects to the original URLs. Use the published ASP template 1.0.151-alpha / Trellis alpha.557 baseline. Begin at AGENTS.md, then the required AgentDocs router and selected recipe bodies; API signatures come from those version-aligned references.
>
> **This lab is deliberately unversioned.** Generate with `--api-versioning false`. No versioning SDK, registration, attributes, route segments or query requirement. `HttpContext.PageUrl`, `CreatedAtRoute` and `WithLocation` belong to the common `Trellis.Asp` surface and need no optional versioning policy. This replaces the old graceful-degradation exercise with package-independent HTTP helpers.

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
| **Anonymous Visitor** | The actor on `GET /{shortCode}` and `GET /health`. Both endpoints declare anonymous access (`[AllowAnonymous]` or equivalent) and do not consult any `links:*` permission. The framework's `IActorProvider` may still yield a default actor in development; the anonymous endpoints simply do not gate on permissions. |

## 3. Aggregates

### 3.1 Link Aggregate

**Identity:** `LinkId` (unique identifier).

**Properties:**
- `ShortCode` — required, unique system-wide, 4–12 chars, `[A-Za-z0-9_-]+`, and not a reserved root segment (`links`, `health`, case-insensitive — these collide with the literal API routes at `/links` and `/health`)
- `OriginalUrl` — required, `OriginalUrl` value object (absolute http/https, ≤2048 chars)
- `OwnerId` — required `OwnerId` value object; captured at creation, immutable
- `CreatedAt` — required UTC timestamp
- `ExpiresAt` — `Maybe<DateTimeOffset>`; when `Some`, must be strictly after `CreatedAt`
- `IsActive` — boolean; defaults to `true`

**Rules:**
- A link cannot be created with `ExpiresAt <= CreatedAt`.
- A link cannot be created with an `OriginalUrl` whose scheme is neither `http` nor `https`.
- A link cannot be created with a `ShortCode` that already exists (storage-layer unique constraint; see §10).
- A link cannot be created with a `ShortCode` equal to a reserved root route segment (`links` or `health`, case-insensitive). Such a code would be shadowed by the literal API route at `/links` or `/health` and the redirect would be unreachable. Rejected at the value-object boundary in `ShortCode.TryCreate`.
- `OwnerId` is immutable after creation.
- `ShortCode` is immutable after creation. (Renaming would break every short link already in circulation.)
- `OriginalUrl` is immutable after creation. (Re-pointing an existing short link to a different destination is out of scope; users mint a new link instead — see §14.)

**Note on open-redirect / SSRF.** The service never fetches the target URL; it only echoes it as the `Location` header on the redirect. `OriginalUrl` accepting `http://localhost/...`, private-network targets, IP-literal hosts, or any other well-formed http/https URL is by design — every shortener is an open-redirect service. SSRF mitigation is not in scope because the service never resolves or de-references the target. Implementations should not invent private-IP blocklists; the spec's only URL rules are the ones in §3.1.

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
- `LinkId` — original link identity, retained even after physical deletion; not an enforced FK to a live `Link`
- `CanonicalRequestJson` — required string; stable JSON serialization of the three input fields the request carried: `(originalUrl, customShortCode, expiresAt)`. Used on replay to detect idempotency-key reuse with a different body (§7, §12).
- `CreatedAt` — required UTC timestamp

**Rules:**
- `(OwnerId, IdempotencyKey)` is unique system-wide.
- An idempotency record is persisted **in the same database transaction** as its corresponding `Link` row. A failure to commit one rolls back the other.
- Deleting the link retains this record as a tombstone. Same key/body with a missing original link returns 404 without recreating it; a different body still returns 409. Intentional new creation requires a new key.

**Operations:** none beyond construction. Records are not mutated. They may be pruned by an out-of-band job after 24 hours; pruning is not in scope for this lab (declared in §14).

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

This service exposes the following endpoints. **There is no `?api-version=` query parameter on any route, and no host-side versioning configuration.** Specifically the host must not register `AddApiVersioning(...)`, must not decorate handlers with `ApiVersion` attributes, must not include `:apiVersion` in any route template, and must not declare `ApiVersionSet`s. Plain `MapGet` / `MapPost` (or attribute-routed `[HttpGet("links")]`) is correct.

Use common `Trellis.Asp` pagination/Location builders, `UseAsp()`, `UseScalarValueValidation()` and `UseProblemDetails()`. Mount scalar validation before endpoints and standard exception/status-code middleware. Do not hand-build links or reimplement ProblemDetails customization.

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| POST   | `/links` | `links:create` | Create a new short link. Optional `Idempotency-Key` header. Returns 201 + `Location`. |
| GET    | `/links` | `links:read` | List the calling actor's links (cursor-paginated). `links:admin` sees all. |
| GET    | `/links/{id}` | `links:read` + owner/admin | Get one link by id. |
| POST   | `/links/{id}/disable` | `links:write` + owner/admin | Disable the link. Returns 200 + `Location: /links/{id}`. Idempotent. |
| PUT    | `/links/{id}/expiry` | `links:write` + owner/admin | Extend `ExpiresAt`. Returns 200 + body. |
| DELETE | `/links/{id}` | `links:write` + owner/admin | Delete a link and clicks, retaining key tombstones (§10). |
| GET    | `/links/{id}/stats` | `links:read` + owner/admin | Click stats projection. Supports `If-None-Match` → 304. |
| GET    | `/{shortCode}` | anonymous | 302 redirect to the original URL. 410 if disabled/expired. 404 if unknown. |
| GET    | `/health` | anonymous | Liveness probe. |

**Route precedence note.** `/{shortCode}` is a wildcard at the root. The service routes must be ordered so that `/links`, `/links/{id}`, `/links/{id}/disable`, `/links/{id}/expiry`, `/links/{id}/stats`, and `/health` win over `/{shortCode}`. The standard ASP.NET Core minimal-API / attribute-routing precedence rules already give literal paths and longer templates priority over single-segment wildcards; the lab does not require any special order declaration, but implementations must not introduce a custom matcher that breaks that default.

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

Response (new link): `201 Created`, `Location: /links/{id}`, body = `LinkView`. Build Location with `CreatedAtRoute("GetLinkById", link => link.Id.Value)` and verify it dereferences.

Response (same key + same canonical body from the same owner): `200 OK`, Location and **current** `LinkView` if the original link still exists. Disable/expiry changes are reflected; no historical response snapshot is stored. If it was deleted, return 404 without creating anything. Compare the canonical request before checking the original link: different-body replay remains 409 after deletion.

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

`shortUrl` is `{scheme}://{host}/{shortCode}` derived from the inbound request's effective scheme and host (i.e., from `HttpRequest.Scheme` and `HttpRequest.Host`). For deployments behind a reverse proxy, the host is assumed to be running with `UseForwardedHeaders` (or equivalent) so that the public host appears in `HttpRequest.Host`. The lab's tests issue requests against the in-memory test host and read whichever scheme/host that produces.

**`POST /links/{id}/disable`**

Permission: `links:write` (owner) or `links:admin`.

Request: empty body.

Response: `200 OK`, plain `Location: /links/{id}`, updated `LinkView`. Build Location with `WithLocation("GetLinkById", link => link.Id.Value)`; it must dereference, with no version parameter.

The operation is idempotent: disabling an already-disabled link is a no-op success (no domain event raised) and still produces 200 + `Location` + the current `LinkView`.

Errors:
- 404 if the link does not exist OR if the caller is not the owner and lacks `links:admin` (existence-leak protection, §6.3).
- 401 / 403 per §9.

**`PUT /links/{id}/expiry`**

Permission: `links:write` (owner) or `links:admin`.

Request:

```json
{ "expiresAt": "2027-12-31T23:59:59Z" }
```

Response: `200 OK`, body = the updated `LinkView`. No `Location` header on this endpoint (the resource URL is the request URL; nothing new to locate).

Errors:
- 422 if `expiresAt` is not strictly later than the link's current `ExpiresAt` (or is not strictly later than `now`).
- 404 per the existence-leak rule.

**`DELETE /links/{id}`**

Permission: `links:write` (owner) or `links:admin`.

Response: `204 No Content`. Cascades to clicks but retains idempotency records (§10).

Errors: 404 per the existence-leak rule.

**`GET /links?cursor=<opaque>&limit=<n>`**

Cursor-based pagination, matching the Trellis `Page<T>` and `HttpContext.PageUrl(...)` shape.

- Parse raw query values with `Request.TryCreatePageRequest(max: 100, defaultSize: 50, policy: PageSizeLimitPolicy.Reject)`. Invalid or repeated values and sizes outside `[1,100]` return coded 422 ProblemDetails (§8).
- Omit `cursor` for the first page; pass the server's opaque `next.cursor` thereafter. A present empty cursor is invalid, not a first page.

Response: `200 OK`, body =

```json
{
  "items": [ /* LinkView, LinkView, ... */ ],
  "requestedLimit": 50,
  "appliedLimit": 50,
  "deliveredCount": 50,
  "wasCapped": false,
  "next": { "cursor": "...", "href": "https://<host>/links?cursor=...&limit=50" }
}
```

This is `PagedResponse<LinkView>`; `next`/`previous` are omitted when unavailable. A final page has no next link.

`Link: <...>; rel="next"` uses the same `HttpContext.PageUrl("ListLinks", ...)` builder as `next.href`. Follow that URL in tests; it must preserve host/path base, contain no api-version parameter and have no gaps/duplicates.

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

Only `GET` is required on `/{shortCode}`. `HEAD`, `OPTIONS`, and other methods produce the framework default `405 Method Not Allowed` with a ProblemDetails body — no special handling required. CORS preflight is out of scope for this lab.

**`GET /health`**

`200 OK` with `{"status": "healthy"}`. No counters, no last-tick state — there is no tick. Always 200 if the process is responsive enough to handle a request.

## 6. Operations (Use Cases)

All operations are implemented as Commands or Queries using CQRS, executed through the mediator pipeline. Every handler returns `Result<T>`; no-payload commands return `Result<Trellis.Unit>` via `Result.Ok()` / `Result.Fail(error)`. The error categorisation (§9) determines the HTTP status the response writer produces.

### 6.0 Implementation pattern: resource authorization

All LinkId-scoped operations (§6.3–§6.7) use shipped resource authorization and checked actor/resource handler parameters:

- Each LinkId-scoped command/query implements `IAuthorizeResource<Link>` + `IIdentifyResource<Link, LinkId>`.
- One `SharedResourceLoaderById<Link, LinkId>` bridges `FirstOrDefaultMaybeAsync` into `Result<Link>` with a lazy `Error.NotFound.For<Link>(id: id.Value)` on miss.
- Scan the **message and loader assemblies**, or register each `AddSharedResourceAuthorization<TMessage,Link,LinkId,TResponse>()` plus the shared loader. Scanning only the Domain assembly cannot discover Application messages or ACL loaders.
- Configure `HideExistence<Link>()`; missing and withheld links share stable public NotFound fields, not per-request traceId/instance values.
- Mutations derive from `ActorResourceCommandHandler`; reads may use `ActorResourceQueryHandler`. Override protected `Handle(message, actor, link, token)`, with only business dependencies in the constructor. No provider re-resolution, accessor plumbing or fallback reload.
- Ownership uses `actor.IsOwner(link.OwnerId.Value) || actor.HasPermission("links:admin")`, not raw permission-set membership. Failure is `Error.Forbidden.For<Link>("links.not-owner-or-admin", id: link.Id.Value)`, translated by the pipeline.
- Retain static `IAuthorize` operation permissions: anonymous → 401; missing base permission → 403; an otherwise permitted non-owner → hidden 404. `links:admin` bypasses ownership/filtering, not the base permission gate.

The sections below describe observable behavior, not manual authorization branches. Tests dispatch through the real Mediator pipeline with fake repositories/actor providers; these bases have no public authorization-bypass overload.

### 6.1 Create Link (Command)

- **Permission required:** `links:create`.
- **Input:** `OriginalUrl`, optional custom code/expiry/key and canonical request JSON (§7). Derive `OwnerId` from the checked actor passed to `ActorCommandHandler`; never trust a client-supplied owner identity.
- **Behaviour:**
  1. Resolve the short code:
     - If `customShortCode` is `Some(s)`, use `s`.
     - Else, generate a new code (system-generated codes are 8 chars, `[A-Za-z0-9]`, drawn from a CSPRNG).
  2. Construct the new `Link` with a freshly allocated `LinkId`.
  3. If `idempotencyKey` is `Some(k)`, also construct `IdempotencyRecord(OwnerId, k, link.Id, canonicalRequestJson, now)`.
  4. An ACL persistence adapter owns an immediate atomic save boundary for both new rows, using `SaveChangesResultAsync` / `SaveChangesWithRetryAsync` rather than duplicating exception mapping. It must resolve uniqueness/replay **before returning** a creation outcome, not defer those decisions to an outer commit. Other mutations stage writes through the normal unit of work.
  5. On `Error.Conflict` with `FaultCodes.DuplicateKey`, distinguish the competing key from a code collision using persisted key lookup/constraint identity in the ACL. Dispose/roll back the failed write context and read in a fresh scope.
     - Matching canonical request + existing original Link → `CreateLinkOutcome.Replayed(currentLink)`.
     - Matching request + deleted original Link → `Error.NotFound.For<Link>(id: existing.LinkId.Value)`, no recreation.
     - Different canonical request → `Error.Conflict.ForReason("idempotency-key-mismatch")`.
  6. A supplied code collision → `Error.Conflict.ForReason("short-code-taken")`. An auto-generated code collision regenerates, up to five total save attempts; exhaustion → `new Error.Unavailable { Code = "short-code-generation-exhausted" }`.
  7. On success, return `Result.Ok(CreateLinkOutcome.Created(link))`.
- **Output:** `Result<CreateLinkOutcome>` where `CreateLinkOutcome` is one of `Created(link) | Replayed(link)`. The HTTP layer maps `Created` to `201` and `Replayed` to `200`.

`TryInsertUniqueAsync` is appropriate for a single clean-context claim, not two separately committed inserts. Do not save Link and key independently. The standard Idempotency-Key response-cache middleware also does not implement this contract: it replays the original status/body, whereas this lab returns 200/current state or 404 after deletion.

### 6.2 List My Links (Query)

- **Permission required:** `links:read`.
- **Input:** `PageRequest`; `ActorQueryHandler` supplies the checked actor.
- **Behaviour:** owner filter (or all links with deny-aware admin permission), then seek/order by `CreatedAt DESC, Id DESC` with a typed `SeekDefinition` and unique tie-breaker.
- **Output:** `Result<Page<LinkView>>`; ASP maps to `PagedResponse<LinkView>` with body/header links from the common `PageUrl` builder.

### 6.3 Get Link By Id (Query)

- **Permission required:** `links:read`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:**
  - Load by id. If not found, return `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.ToString())))`.
  - If `link.OwnerId != actor.Id` and the actor does not have `links:admin`, return `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.ToString())))` — not `Error.Forbidden`. (Surfacing "this link exists but you can't see it" leaks existence.)
  - Otherwise return `Result.Ok(LinkView)`.

### 6.4 Disable Link (Command)

- **Permission required:** `links:write`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:**
  - Load + ownership-check per §6.3 (`new Error.NotFound(ResourceRef.For<Link>(id.ToString()))` if not owner and not admin).
  - Call `link.Disable()`. The aggregate's `Disable()` is idempotent; if `IsActive` is already `false`, no event is raised and `SaveChanges` does no write.
  - Return `Result.Ok(LinkView)` with the post-call state (`isActive: false`).
- **HTTP layer:** common `WithLocation("GetLinkById", link => link.Id.Value)` emits a plain dereferenceable link.

### 6.5 Extend Link Expiry (Command)

- **Permission required:** `links:write`.
- **Input:** `LinkId`, `actor`, `expiresAt: DateTimeOffset`.
- **Behaviour:**
  - Load + ownership-check per §6.3.
  - Call `link.Extend(expiresAt)`. Invalid extension yields `Error.InvalidInput.ForField("link.expiry.invalid", "expiresAt", detail: "...")`; no mutation.
  - On success, persist and return `Result.Ok(LinkView)`.

### 6.6 Delete Link (Command)

- **Permission required:** `links:write`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:** Consume the pipeline-authorized link; delete it and its clicks, **retain** all associated idempotency records. Return `Result.Ok()` (`Result<Trellis.Unit>`). An old-key/same-body retry returns 404; old-key/different-body remains 409. A new key allows intentional new creation.

### 6.7 Get Link Stats (Query)

- **Permission required:** `links:read`.
- **Input:** `LinkId`, `actor`.
- **Behaviour:**
  - Ownership-check per §6.3 first — before any ETag work. A non-owner without `links:admin` receives `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.ToString())))` regardless of any `If-None-Match` header the caller sent. The ETag for a link is never echoed to a caller who is not entitled to see the link.
  - Compute `LinkStats` from the `Clicks` table: `TotalClicks = COUNT(*)`, `FirstClickAt = MIN(ClickedAt)`, `LastClickAt = MAX(ClickedAt)`. A single aggregate-projection query, not a load-all-clicks-then-aggregate.
  - Return `Result.Ok(LinkStatsView)`.
- **HTTP layer:** computes ETag deterministically from `(TotalClicks, LastClickTicks)`. If the request's `If-None-Match` matches AND the caller passed the ownership check, respond `304 Not Modified` and skip body serialisation entirely.

### 6.8 Redirect (Query) + Record Click (Command)

- **Permission required:** none (anonymous).
- **Input:** `ShortCode`, optional `UserAgent`, optional `Referer`.
- **Behaviour:**
  - Lookup by `ShortCode`. Not found → `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(shortCode.Value)))`. HTTP layer maps to `404`.
  - Eligible (active, not expired) → emit `RedirectOutcome.Redirect(originalUrl)`. HTTP layer maps to `302` with `Location` header.
  - Disabled or expired → emit `RedirectOutcome.Gone`. HTTP layer maps to `410 Gone`.
  - On `Redirect` (only): record a click via `RecordClickCommand`. The orchestrator wraps the click-recording call in a `try/catch`; any exception is logged at Warning and swallowed. The response status code is unaffected.
- **`RecordClickCommand`** is a small handler taking `LinkId`, `now`, `UserAgent?`, `Referer?` and appending one `Click` row. Permission is none (it is internal to the redirect orchestrator and never reached from an HTTP route directly).

### 6.9 Health (Query)

Anonymous. Returns `{ "status": "healthy" }`. Always `200`. There is no underlying check beyond "the process can handle a request" — Trellis `IHealthCheck` integration is out of scope for this lab.

## 7. Idempotency

Idempotency applies only to `POST /links`. All other writes (`POST /links/{id}/disable`, `PUT /links/{id}/expiry`, `DELETE /links/{id}`) are naturally idempotent at the resource level: repeating them with the same body produces the same state.

The contract for `POST /links`:

- **No `Idempotency-Key` header:** create a new link on every call. Two POSTs with the same body produce two links with different short codes (unless `customShortCode` is supplied — then the second call returns `Error.Conflict`).
- **`Idempotency-Key` header present:**
  - On first observation of `(OwnerId, key)`: create the link and the idempotency record in one transaction. Persist `CanonicalRequestJson` on the record. Respond `201 Created`.
  - Same canonical request: no mutation; return 200/current LinkView + Location if the original link exists, or 404 if it was deleted. Disabling/extending it changes the replay view, not the stored request.
  - Different canonical request: 409 (`Error.Conflict.ForReason("idempotency-key-mismatch")`), including after deletion.
  - Link identity remains stable across replays. Response bytes need not match the original creation snapshot; shortUrl uses the current effective scheme/host.

**Canonical-JSON serialization.** `CanonicalRequestJson` is built deterministically from the three input fields `(originalUrl, customShortCode, expiresAt)` so that two requests with the same intent but different JSON whitespace or key ordering compare equal. Implementations may use `System.Text.Json` with `JsonSerializerOptions { WriteIndented = false }` and a fixed property order (or any equivalent canonicalization scheme), as long as the comparison is stable across calls. The lab tests `idempotent replay with reordered keys` to pin this.

**Why the record is per-owner, not global.** Idempotency keys are opaque client-supplied strings. Two unrelated clients may legitimately use the same key (e.g., a UUID generator that wraps, or a fixed retry-token). Scoping to `(OwnerId, key)` prevents one client's retries from masking another client's creates.

**Storage-layer contract.** The `(OwnerId, IdempotencyKey)` uniqueness must be enforced by a database constraint, not by a read-then-decide check in the handler. A read-then-decide check races across concurrent retries from the same client.

## 8. Pagination

The list endpoint (`GET /links`) is cursor-paginated and exercises Trellis's `Page<T>` shape:

- `TryCreatePageRequest(max: 100, defaultSize: 50, policy: PageSizeLimitPolicy.Reject)` reads raw query input; do not add bound cursor/limit parameters merely for OpenAPI.
- Missing cursor means first page; empty/whitespace/repeated/malformed cursor → 422 `cursor.malformed`. Empty/malformed/overflow/repeated limit → 422 `format.integer`; parsed values outside `[1,100]` → 422 `page-size.out-of-range`.
- `PageRequest` flows unchanged through the query/repository. Typed seek uses the same descending timestamp/ID boundary as ordering; never seek after limiting.
- The ASP body is `PagedResponse<LinkView>` (§5.1). Body/header next URLs come from common `HttpContext.PageUrl`.
- Declare OpenAPI cursor/limit metadata separately and use `[InputOrigin(InputLocation.Query)]` (or Minimal API equivalent) for downstream cursor failures. Assert `fieldViolations[].code` and query `location`, not just 422.
- The `Link` header is absent on the last page.

The choice of cursor (not numbered `page`/`pageSize`) is deliberate: `HttpContext.PageUrl(...)` is a cursor-based helper (returns `Func<Cursor, int, string>`). Numbered pagination would force hand-built URLs, defeating the PageUrl measurement.

## 9. Authorization

**Permissions (claim type: `permission`):**
- `links:create` — required to POST a link.
- `links:read` — required to GET own links and own stats.
- `links:write` — required to POST `/disable`, PUT `/expiry`, and DELETE.
- `links:admin` — bypasses ownership filtering; sees and manages all links.

**Actor resolution.** The host registers Trellis's HTTP `IActorProvider` — the standard `DevelopmentActorProvider` for the lab; a real IdP-backed provider in production. There is exactly one `IActorProvider` registration.

**Anonymous endpoints (`/{shortCode}` and `/health`):**
- These endpoints declare anonymous access — typically via `[AllowAnonymous]` on the controller method or by routing them outside the authorization-required pipeline.
- They do not consult any `links:*` permission.
- The framework's `IActorProvider` may still yield a default actor in development (the template's `DevelopmentActorProvider` returns a default actor when no `X-Test-Actor` header is present; this is by design, see `Trellis.Asp.Authorization.DevelopmentActorProvider`). Anonymous endpoints simply do not read permissions off that actor; the request is served regardless of who (or whether) the actor provider reports.
- An authenticated request to `/{shortCode}` is treated identically to an anonymous one. The redirect is independent of identity.

**Ownership-versus-existence.** Static operation permissions remain required; admins hold the appropriate base permissions too. Resource authorization uses `HasPermission` (deny overrides allow), then `HideExistence<Link>()`. For otherwise permitted callers, missing/withheld links have identical stable public status/kind/code/resource/detail, excluding per-request instance/traceId. Do not manually hide existence in each handler.

## 10. Persistence

- **Database:** SQLite (file-based, zero setup).
- **Connection string** in `appsettings.Development.json`: `Data Source=UrlShortener.db`.
- **Entities to persist:** `Link`, `Click`, `IdempotencyRecord`.
- **Unique constraints:**
  - `Links(ShortCode)` — unique. Enforces global short-code uniqueness.
  - `IdempotencyRecords(OwnerId, IdempotencyKey)` — composite unique.
- **Indexes:**
  - `Links(OwnerId, CreatedAt DESC, Id DESC)` — supports deterministic owner-scoped seek ordering.
  - `Clicks(LinkId, ClickedAt)` — supports stats aggregation.
- **Cascade:**
  - Deleting a `Link` row cascades to its `Click` rows. Configured via EF Core `OnDelete(DeleteBehavior.Cascade)` on the `Link → Clicks` relationship.
  - Idempotency records are **not** cascaded. Original LinkId is stored as an identity value without a mandatory live-row FK, allowing tombstones to survive deletion. Owner/key/request/original LinkId stay intact.
- **Enums** (none in this domain — `IsActive` is bool).
- **`Maybe<T>` columns** (`ExpiresAt`, `UserAgent`, `RefererHost`) stored as nullable columns, round-tripping to `Maybe.None` when null.
- **Database creation:** Use `EnsureCreated()` on startup in development mode. Do NOT use EF Core migrations.

**Click persistence in the redirect path.** The `Click` insert must not propagate any error that changes the redirect status code. The redirect orchestrator wraps the click-persistence call in a `try/catch` and logs failures at Warning. Implementations may use a fresh DI scope, a no-tracking write context, or simply a wrapping try/catch — the binding contract is "click failure does not change the redirect status code" (§3.2).

## 11. Caching

`GET /links/{id}/stats` is the only cached endpoint.

- `ETag: "{totalClicks}-{lastClickTicks}"` (strong validator). `lastClickTicks = LastClickAt?.UtcTicks ?? 0`.
- `Cache-Control: private, max-age=60` on the `200` response.
- `If-None-Match: "..."` matching the current ETag → `304 Not Modified`, no body, no `Cache-Control` header (the cached entry is still valid; the proxy/browser already has the headers).
- `If-None-Match: "*"` matches any current ETag and behaves the same way.
- The handler must short-circuit before serialising the body on a 304. The lab does not require any specific framework helper for ETag (Trellis's `WithCacheControl` and ETag helpers are encouraged but not mandated); the binding contract is the externally observable behaviour.

**Authorization precedes ETag evaluation.** A non-owner without `links:admin` requesting `/links/{id}/stats` always receives `404 Not Found` regardless of the `If-None-Match` header. The handler resolves ownership first; if the caller is not entitled to see the link, no ETag comparison happens and no 304 is ever emitted to that caller. This is the existence-leak protection (§6.3) applied to the cached endpoint — without it, a malicious client could probe link existence by replaying an ETag they obtained elsewhere and observing 304-vs-404.

The cache validator is **not** a strong concurrency guard for writes. There is no `If-Match` round-trip on stats (stats is read-only). The validator exists to save bandwidth on polling clients.

## 12. Error Behavior

`ToHttpResponse` / `AsActionResult` map Results to HTTP through `UseAsp()`; `UseProblemDetails()` enriches errors and standard exception/status-code middleware handles host failures. There is no `UseTrellisProblemDetails` middleware. The full table:

| Situation | `Error` returned by handler | HTTP status | ProblemDetails type |
|-----------|------------------------------|-------------|---------------------|
| Anonymous request to a `links:*` endpoint | (none — auth middleware short-circuits before the handler runs) | `401 Unauthorized` | `Error.AuthenticationRequired` |
| Authenticated request missing the required permission | `Error.Forbidden(policyId, resource?)` from the auth filter | `403 Forbidden` | `Error.Forbidden` |
| `POST /links` body invalid (`originalUrl` missing, scheme not http/https, `customShortCode` regex fail) | `Error.InvalidInput.ForField(...)` | `422 Unprocessable Entity` | `Error.InvalidInput` |
| Custom short-code collision | `Error.Conflict.ForReason("short-code-taken")` | 409 | `Error.Conflict` |
| Same key / different canonical request | `Error.Conflict.ForReason("idempotency-key-mismatch")` | 409 | `Error.Conflict` |
| Same key / same request / deleted original link | `Error.NotFound.For<Link>(id: originalLinkId)` | 404 | `Error.NotFound` |
| Auto-code collision retries exhausted | `new Error.Unavailable { Code = "short-code-generation-exhausted" }` | 503 | `Error.Unavailable` |
| Invalid cursor/limit query (§8) | located `Error.InvalidInput` | 422 | `Error.InvalidInput` |
| `GET /links/{id}` where id doesn't exist OR is owned by another non-admin actor | `new Error.NotFound(ResourceRef.For<Link>(id))` | `404 Not Found` | `Error.NotFound` |
| `GET /{shortCode}` where short-code doesn't exist | `new Error.NotFound(ResourceRef.For<Link>(shortCode))` | `404 Not Found` | `Error.NotFound` |
| `GET /{shortCode}` where link is disabled or expired | `new Error.Gone(ResourceRef.For<Link>(shortCode))` | `410 Gone` | `Error.Gone` |
| Invalid expiry extension | `Error.InvalidInput.ForField("link.expiry.invalid", "expiresAt", detail: "...")` | 422 | `Error.InvalidInput` |

**On "idempotency-key-mismatch".** The strictest behaviour from RFC draft `idempotency-header-01` is: if the same key is reused with a *different* request body, reject with `409 Conflict`. The lab requires this strict behaviour. The `IdempotencyRecord` persists a `CanonicalRequestJson` field (§3.3) — a stable JSON serialization of the three input fields (`originalUrl`, `customShortCode`, `expiresAt`) — at the time of the original `201`. On replay, the handler compares the stored string against the canonical-JSON serialization of the current request. Equality → replay (200). Inequality → 409. Identity comparison on `OwnerId` is implicit (records are scoped per owner).

**On `Error.Gone`.** Trellis ships `Error.Gone(ResourceRef Resource)` as a first-class case in the `Error` taxonomy. Disabled/expired links must surface as `new Error.Gone(ResourceRef.For<Link>(shortCode))`, which the `AddTrellisProblemDetails` pipeline maps to `410 Gone` with the correct ProblemDetails shape. A 404 fallback for disabled/expired links is **not** acceptable — collapsing 410 into 404 hides the lifecycle-state signal from clients and is a deduction under §13.3 in the response-shape rubric.

## 13. Testing Requirements

> **Coverage bar:** the prose below summarises the test categories. The full per-row coverage matrix lives in [`coverage-checklist-url-shortener.md`](./coverage-checklist-url-shortener.md) and is the binding stop-criterion: every row in §1–§9 of that checklist must have at least one matching assertion.

### 13.1 Domain Tests

Unit tests for value objects and aggregate rules. No external dependencies.

- Value objects: `ShortCode`, `OriginalUrl`, `IdempotencyKey`, `UserAgent`, `RefererHost`, every identity type. Each tests `TryCreate` happy path, boundary conditions, null/empty, format violations, equality.
- `Link`: valid creation, `ExpiresAt > CreatedAt` rule, `OriginalUrl` scheme rule, `Disable`/`Enable` idempotency (no event on no-op call), `Extend` rule (must be strictly later than current and strictly after now), immutability of `OwnerId`/`ShortCode`/`OriginalUrl` (no setters or only-internal setters).
- `Click`: construction; `UserAgent` and `RefererHost` accept `Maybe<None>` and `Maybe<Some>`.

### 13.2 Application Tests

Handler tests with fake repositories.

- `CreateLinkCommand`: auto/custom creation, collision/regeneration/exhaustion, atomic key+link save and same/different/reordered requests. After disable/extension replay the current view. After deletion retain the key: same request → 404; different request → 409; new key → 201.
- `ListMyLinksQuery`: owner/admin filters and deterministic `CreatedAt DESC, Id DESC` pagination, including tied timestamps.
- `GetLinkByIdQuery`: own link returns 200; another's link returns `Error.NotFound` (not `Error.Forbidden`); admin sees another's link.
- `DisableLinkCommand`: idempotent (second call is no-op success, no event); ownership check enforces `Error.NotFound` for non-owner non-admin.
- `ExtendLinkExpiryCommand`: extends expiry; rejects new value not strictly later than current; rejects new value not strictly later than now; ownership check enforces `Error.NotFound`.
- `DeleteLinkCommand`: deletes own link; cascades to clicks (verified at the persistence layer in §13.4); non-owner gets `Error.NotFound`.
- `GetLinkStatsQuery`: zero-click projection (`firstClickAt`/`lastClickAt` are `None`); non-zero projection; ownership precedes stats computation (a non-owner request returns `Error.NotFound` without touching the projection query).
- `RedirectQuery`: 302 outcome for eligible link; `Gone` outcome for disabled link; `Gone` outcome for expired link; `NotFound` for unknown short code.
- `RecordClickCommand`: appends a row; failure does not bubble (handler swallows and logs); recorded fields match input.
- Authorisation: every command/query succeeds with an actor holding the required permission; fails with an actor lacking it. The redirect endpoint and health endpoint succeed for any caller (the framework's default-actor behavior in development is acceptable; the lab does not require `Maybe<Actor>.None` at the provider).

### 13.3 HTTP Integration Tests

Use `WebApplicationFactory` as normal.

- **The unversioned-host contract** (the lab's central test):
  - All routes are reachable **without** an `?api-version=` query parameter.
  - Adding an `?api-version=1.0` query parameter to any route is either a no-op (accepted and ignored) or produces a framework-defined 400 — the lab does not require a specific outcome, but the test documents whichever the implementation chose.
  - No versioning registration or optional SDK dependency. The published template defaults unversioned; use `--api-versioning false`.
  - No handler signature includes an `ApiVersion` parameter, and no route template contains `:apiVersion`.
- `POST /links`: 201 with dereferenceable plain Location; 422 invalid body; 409 code/key mismatch; 200 replay/current view; 404 replay after deletion without resurrection. New key after deletion → 201.
- `POST /links/{id}/disable`: 200 OK with `Location: /links/{id}` (no `?api-version=`); 404 for non-owner non-admin; idempotent (second call still 200 with the same body).
  - Follow the common `WithLocation` URL; no version parameter, same authorized link.
- `GET /links`: 200 with body + `Link` header. The `Link` header value must not contain `api-version=`. Pagination via `cursor`/`limit` round-trips via `HttpContext.PageUrl(...)`.
  - Follow next.href and the Link-header URL; prove no gaps/duplicates for tied timestamps. Invalid raw queries return correct coded/location 422, and OpenAPI declares cursor/limit independently.
- `GET /links/{id}`: own link 200; another's link 404 (not 403); admin sees another's 200.
- `PUT /links/{id}/expiry`: extension persists and round-trips; invalid expiry returns 422.
- `DELETE /links/{id}`: 204; subsequent GET returns 404; clicks gone (§13.4).
- `GET /links/{id}/stats`: 200 with ETag header. Second request with `If-None-Match: <same etag>` → 304 with empty body. Mutating state (a click in between) changes the ETag. A non-owner request with `If-None-Match: <ETag obtained out-of-band>` returns 404, NOT 304 (existence-leak protection on the cached endpoint, §11).
- `GET /{shortCode}`: 302 with `Location: <originalUrl>` for active link; 410 for disabled; 410 for expired; 404 for unknown. Anonymous access — no `Authorization` header on the request.
  - A click row is persisted on 302 (verified by reading the `Clicks` table after the response).
  - A click row is **not** persisted on 410 or 404.
  - `HEAD /{shortCode}` returns the framework default 405 (no special handling). The lab does not measure HEAD behaviour beyond "the redirect endpoint is not magically also a HEAD handler."
- `GET /health`: 200 with anonymous access.
- ProblemDetails: RFC 9457, `application/problem+json`, stable kind/code/violations and per-request traceId. Do not use `[Produces("application/json")]` to overwrite the error media type.

### 13.4 Persistence Tests

Round-trip tests against real SQLite (file or shared in-memory connection — not the in-memory EF Core provider, so unique-constraint behaviour is exercised).

- `Link` insert + reload: every property (`ExpiresAt` absent + present, `IsActive`, `OwnerId`, `OriginalUrl`, `ShortCode`) survives a save + reload.
- `Click` insert + reload: `UserAgent` absent + present, `RefererHost` absent + present, `ClickedAt`.
- Idempotency records round-trip and survive physical Link deletion with owner/key/request/original LinkId intact. Same-body replay → 404, different-body → 409; clicks still cascade.
- Unique constraint: `Links(ShortCode)` rejects a duplicate.
- Unique constraint: `IdempotencyRecords(OwnerId, IdempotencyKey)` rejects a duplicate (same owner, same key); accepts the same key from a different owner.
- Cascade: deleting a `Link` deletes its `Click` rows.
- Index: `Links(OwnerId, CreatedAt DESC, Id DESC)` is declared (model inspection).
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
