# Test Coverage Checklist — URL Shortener (v1)

Companion to `specs/url-shortener.md`. This checklist makes the expected test coverage explicit and machine-checkable so models stop at "rubric coverage" rather than "representative happy + key failure paths."

Each row should be a separate test (or a single parameterised test with named cases). A row is **green** when the implementation has at least the listed **positive** and **negative** assertions.

Where it differs from `coverage-checklist.md` (the OM checklist): there are dedicated sections for the unversioned-host contract (§7), the redirect endpoint and its anonymous-access shape (§7), idempotency on POST (§4), and ETag round-trip (§7). The OM checklist's API-versioning rows (e.g., "request without `?api-version=` returns 400") have no equivalent here — by design.

Where it differs from `coverage-checklist-subscription-reminder.md` (the worker checklist): there is no §5 gateway-error mapping, no §4 worker-tick orchestration, no §3.3 counter-invariant. This service is HTTP-shaped end-to-end.

## Required eval minimum (subset)

The rubric L4 (`docs/evaluation-criteria.md` Level 4) actually scores against this minimum subset. Rows outside the minimum are extended completeness — required for "test-complete" but not individually scored.

| § | Minimum row |
|---|---|
| §1 | Every scalar VO: `TryCreate` happy path + at least one boundary failure + null/empty failure |
| §1 | Reused Trellis built-ins integrate cleanly into `Link` — invalid input rejected at aggregate construction |
| §2 | `Link.Disable` / `Link.Enable` idempotency — second call is no-op success, **no domain event raised** the second time |
| §2 | `Link.Extend` rejects new `ExpiresAt` ≤ current `ExpiresAt` |
| §3 | `Link`: `ExpiresAt <= CreatedAt` rejected; `OriginalUrl` scheme non-http(s) rejected |
| §3 | `Link`: `OwnerId`, `ShortCode`, `OriginalUrl` immutable after creation (verified by reflection or by absence of public setters) |
| §4 | `CreateLinkCommand`: happy path with auto-generated code + happy path with custom code + collision on custom code + idempotency-key replay returns same `LinkId` + idempotency-key mismatch returns `Error.Conflict` |
| §4 | `GetLinkByIdQuery`: another's link returns `Error.NotFound`, not `Error.Forbidden` (existence-leak protection) |
| §4 | `RedirectQuery`: disabled link → `Gone`; expired link → `Gone`; eligible → `Redirect(originalUrl)`; unknown → `NotFound` |
| §4 | `RecordClickCommand`: failure does not bubble out of the redirect orchestrator (the response status is unchanged) |
| §6 | `(OwnerId, IdempotencyKey)` storage-layer unique constraint produces a unique-violation that the handler catches and translates into a replay (200) — not a 500 |
| §7 | **Unversioned-host contract:** `Program.cs` does not call `AddApiVersioning(...)`; no route has `?api-version=` requirement; every endpoint reachable without that query parameter |
| §7 | **`WithLocation` test:** `POST /links` 201 response's `Location` header is `/links/{id}` with no query parameters appended |
| §7 | **`HttpContext.PageUrl` test:** `GET /links` paginated response's `Link` header values are well-formed and contain no `api-version=` query parameter |
| §7 | `GET /links/{id}` returns 404 (not 403) when the caller is not the owner and not an admin |
| §7 | `GET /{shortCode}`: 302 with `Location` for eligible link, 410 for disabled, 410 for expired, 404 for unknown — all anonymous (no `Authorization` header) |
| §7 | `GET /links/{id}/stats`: second request with `If-None-Match: <prior etag>` returns 304 with empty body; intervening click changes the ETag |
| §7 | A click row is persisted on 302 redirect; **no** click row is persisted on 410 or 404 |
| §9 | `Links(ShortCode)` unique constraint exercised against real SQLite; second insert raises EF Core unique-violation |
| §9 | `Link` → `Click` cascade delete works (clicks gone after `Link` deleted) |

## Extended completeness

Everything below is required for "test-complete" but not individually scored by L4.

## 1. Scalar value objects (`Domain/tests`)

For every scalar VO declared in the spec — `ShortCode`, `OriginalUrl`, `IdempotencyKey`, `UserAgent`, `RefererHost`, and the strongly-typed identity types (`LinkId`, `OwnerId`, `ClickId`):

| Coverage | Required |
|---|---|
| `TryCreate` happy path | ≥1 valid input returns `Result.Ok` and round-trips |
| `TryCreate` boundary low | minimum-length input returns `Result.Ok` |
| `TryCreate` boundary high | maximum-length input returns `Result.Ok` |
| `TryCreate` below low | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` above high | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` null/empty/whitespace | `Result.Fail` |
| Format / pattern violation | `Result.Fail`. `ShortCode` rejects characters outside `[A-Za-z0-9_-]`. `OriginalUrl` rejects non-http(s) schemes (`ftp://...`, `javascript:...`, relative URLs). `RefererHost` rejects strings that fail DNS-hostname format. `UserAgent` and `IdempotencyKey` are opaque — no format row required beyond length. |
| Equality and `GetHashCode` | two VOs with identical inputs are equal; differing inputs are not equal |

Reused Trellis built-ins (e.g., the framework's URL value object if used) do **not** need re-testing of their internal pattern rules — only that they integrate correctly into `Link`. Verify integration by constructing `Link` with a bad URL and asserting the failure surfaces with `Error.InvalidInput` for the right field.

## 2. State machine (`Domain/tests`)

For every transition on `Link` declared in spec §4:

| Coverage | Required |
|---|---|
| `Active → Disabled` happy path | status updated; `LinkDisabledDomainEvent` raised |
| `Disabled → Active` happy path | status updated; `LinkEnabledDomainEvent` raised |
| `Disable` on already-disabled | no-op success; **no event raised the second time** |
| `Enable` on already-active | no-op success; **no event raised the second time** |
| `Extend` happy path | `ExpiresAt` updated; `LinkExpiryExtendedDomainEvent` raised |
| `Extend` with `newExpiresAt <= current ExpiresAt` | `Result.Fail` with `Error.InvalidInput`; no state mutation |
| `Extend` with `newExpiresAt <= now` | `Result.Fail` with `Error.InvalidInput`; no state mutation |
| `Extend` on link with no current `ExpiresAt` | succeeds when `newExpiresAt > now` |
| `OwnerId` immutability | no public setter; reflection-based attempt to set raises or has no public path |
| `ShortCode` immutability | as above |
| `OriginalUrl` immutability | as above |

## 3. Aggregate invariants (`Domain/tests`)

| Coverage | Required |
|---|---|
| `Link`: `ExpiresAt <= CreatedAt` | rejected at construction; `Result.Fail` with `Error.InvalidInput` |
| `Link`: `OriginalUrl` scheme `ftp` | rejected at construction (via `OriginalUrl.TryCreate`) |
| `Link`: `OriginalUrl` scheme `javascript` | rejected at construction (existence rationale: XSS surface; this is a domain rule, not a framework concern) |
| `Link`: `ShortCode` regex violation | rejected at construction |
| `Link`: empty `OwnerId` | rejected at construction |
| `Click`: required fields present | construction with `LinkId`, `ClickedAt` succeeds; both `UserAgent` and `RefererHost` accept `None` |
| `IdempotencyRecord`: required composite identity | construction requires both `OwnerId` and `IdempotencyKey`; either missing fails |

## 4. Command and query handlers (`Application/tests`)

For `CreateLinkCommand`:

| Coverage | Required |
|---|---|
| Happy path — auto-generated code | `Link` persisted; `CreateLinkOutcome.Created(link)`; `ShortCode` matches generator constraints (length, charset) |
| Happy path — custom code | persisted; `CreateLinkOutcome.Created(link)` with the requested `ShortCode` |
| Custom code already taken | `Result.Fail(Error.Conflict)`; no `Link` row persisted; no `IdempotencyRecord` row persisted |
| Auto-code collision and regeneration | fake generator returns a colliding code N times then a fresh one; final outcome is `Created`; exactly one `Link` row persisted |
| Auto-code regeneration exhausted | fake generator always collides; outcome is `Result.Fail(Error.Unavailable("short-code-generation-exhausted"))`; no `Link` row persisted; no orphan `IdempotencyRecord` |
| Idempotency-key first observation | `IdempotencyRecord` and `Link` both persisted in one transaction; outcome `Created` |
| Idempotency-key replay (same body) | second call returns `Replayed(link)` with the same `LinkId`; no new `Link` or `IdempotencyRecord` row; no new domain event raised |
| Idempotency-key reuse with different body | second call returns `Result.Fail(Error.Conflict)`; no mutation |
| Idempotency-key from a **different owner** with the same key | succeeds; produces a distinct `Link` and a distinct `IdempotencyRecord` |
| No idempotency-key, repeated identical request | each call produces a distinct `Link` (auto-generated codes differ) |
| Transactional atomicity | simulate a database failure between the `Link` insert and the `IdempotencyRecord` insert; assert neither row persists |

For `ListMyLinksQuery`:

| Coverage | Required |
|---|---|
| Owner-scoped filter | returns only the caller's links; admin sees all |
| Pagination | `page` and `pageSize` honoured; `TotalCount` reflects unfiltered (per-owner-or-admin) total |
| Ordering | results ordered by `CreatedAt DESC` |
| `pageSize` out of range | `Error.InvalidInput.ForField("pageSize", ...)` |
| `page = 0` | `Error.InvalidInput.ForField("page", ...)` |

For `GetLinkByIdQuery`:

| Coverage | Required |
|---|---|
| Own link | 200 with `LinkView` |
| Another's link, no `links:admin` | `Result.Fail(Error.NotFound)` — **not** `Error.Forbidden` |
| Another's link, with `links:admin` | 200 with `LinkView` |
| Unknown id | `Result.Fail(Error.NotFound)` |

For `UpdateLinkCommand`:

| Coverage | Required |
|---|---|
| Toggle active → disabled | persisted; `LinkDisabledDomainEvent` raised; `IsActive` round-trips |
| Toggle disabled → active | persisted; `LinkEnabledDomainEvent` raised |
| No-op toggle (same state) | succeeds with no event raised; `LinkView` returned reflects unchanged state |
| Extend expiry | `LinkExpiryExtendedDomainEvent` raised; `ExpiresAt` updated |
| Extend with invalid value | `Result.Fail(Error.InvalidInput)`; no mutation |
| Non-owner update | `Result.Fail(Error.NotFound)` (not `Forbidden`) |
| Admin update on another's link | succeeds |

For `DeleteLinkCommand`:

| Coverage | Required |
|---|---|
| Own link | deleted; subsequent `GetLinkByIdQuery` returns `Error.NotFound` |
| Cascade to clicks | a `Link` with N clicks deletes both the link row and all N click rows |
| Non-owner delete | `Result.Fail(Error.NotFound)`; link still exists |
| Admin delete on another's link | succeeds |

For `GetLinkStatsQuery`:

| Coverage | Required |
|---|---|
| Zero clicks | `TotalClicks = 0`; `FirstClickAt = None`; `LastClickAt = None` |
| Multiple clicks | `TotalClicks` correct; `FirstClickAt = MIN`; `LastClickAt = MAX` |
| Single click | `FirstClickAt == LastClickAt` |
| Non-owner | `Error.NotFound` (existence-leak rule) |
| Admin | sees another's stats |

For `RedirectQuery`:

| Coverage | Required |
|---|---|
| Unknown short code | `RedirectOutcome.NotFound` (the orchestrator translates to `Error.NotFound`) |
| Active link, not expired | `RedirectOutcome.Redirect(originalUrl)` |
| Disabled link | `RedirectOutcome.Gone` |
| Expired link | `RedirectOutcome.Gone` |
| Active link with future `ExpiresAt` | `RedirectOutcome.Redirect(...)` |

For `RecordClickCommand`:

| Coverage | Required |
|---|---|
| Happy path | one `Click` row appended with correct `LinkId`, `ClickedAt`, `UserAgent`, `RefererHost` |
| Persistence failure | exception caught; logged at Warning; **does not propagate** to caller |
| Missing `User-Agent` header | row persists with `UserAgent = None` |
| Malformed `Referer` header | row persists with `RefererHost = None`; no exception |
| `Referer` with hostname > 253 chars | row persists with `RefererHost = None` (truncated to None, not stored truncated) |

Authorisation matrix (run as a parameterised test per handler):

| Handler | Required permission | Anonymous (`Maybe.None`) | Authenticated, lacks permission | Has permission |
|---|---|---|---|---|
| `CreateLinkCommand` | `links:create` | 401 | 403 | 200/201 |
| `ListMyLinksQuery` | `links:read` | 401 | 403 | 200 |
| `GetLinkByIdQuery` | `links:read` | 401 | 403 (no permission) or 404 (has permission but not owner+not admin) | 200 |
| `UpdateLinkCommand` | `links:write` | 401 | 403 (no permission) or 404 (has permission but not owner+not admin) | 200 |
| `DeleteLinkCommand` | `links:write` | 401 | 403 / 404 | 204 |
| `GetLinkStatsQuery` | `links:read` | 401 | 403 / 404 | 200 |
| `RedirectQuery` (via `/{shortCode}`) | none | 302 / 410 / 404 | 302 / 410 / 404 | 302 / 410 / 404 |
| Health | none | 200 | 200 | 200 |

## 5. Idempotency (`Application/tests` + `Acl/tests`)

| Coverage | Required |
|---|---|
| Storage-layer composite unique | `(OwnerId, IdempotencyKey)` second insert raises EF Core unique-violation; handler catches and reports `Replayed(link)` |
| Cross-owner key reuse | same key from a different `OwnerId` succeeds; produces a distinct `IdempotencyRecord` |
| Body-equivalence comparison | replays with a body that differs in ordering of keys are still treated as equivalent (canonical-JSON comparison); replays with a body that differs in a value field are rejected with `Error.Conflict` |
| Transactional all-or-nothing | a forced failure on the `Link` insert (after the `IdempotencyRecord` insert) rolls back both; a subsequent retry with the same key succeeds as a fresh first-observation |
| Concurrent first-attempt race | two simulated concurrent first-attempt POSTs with the same `(OwnerId, key)` → one succeeds with `Created`; the other catches the unique-violation and returns `Replayed` with the same `LinkId` |

## 6. Caching (`Api/tests`)

| Coverage | Required |
|---|---|
| 200 response carries `ETag` | header present, strong validator, format `"{totalClicks}-{lastClickTicks}"` |
| 200 response carries `Cache-Control: private, max-age=60` | header present and exact |
| `If-None-Match` matching current ETag → 304 | empty body; **no `Cache-Control` header** on the 304 |
| `If-None-Match: "*"` → 304 | matches any current ETag |
| `If-None-Match` not matching → 200 | full body with current ETag |
| ETag changes after a click | the ETag value after a click is different from before |
| ETag stable across reads with no intervening change | two consecutive reads yield the same ETag |

## 7. HTTP endpoints (`Api/tests`)

### 7.1 Unversioned-host contract (the lab's central test)

| Coverage | Required |
|---|---|
| `Program.cs` composition | does not invoke `AddApiVersioning(...)`; verified by inspecting the host's `IServiceCollection` for absence of versioning types, or by source-grep against `Program.cs`/`DependencyInjection.cs` |
| No `?api-version=` requirement | every route is reachable with the bare URL — `GET /links`, `POST /links`, `GET /links/{id}`, `GET /links/{id}/stats`, `GET /{shortCode}`, `GET /health` |
| `?api-version=1.0` appended to any route | accepted and ignored (no 400) **or** produces a framework-defined 400 — the lab does not mandate one, but the test documents whichever outcome the implementation produces |
| `WithLocation` produces a clean Location | `POST /links` 201 response has `Location: /links/{id}` with **no** query parameters (no `?api-version=1.0` injection) |
| `HttpContext.PageUrl` produces clean Link header | `GET /links` paginated response's `Link` header values contain **no** `api-version=` query parameter |
| ProblemDetails type URIs | `type` field on error responses does not reference an api-version-specific path |
| No `ApiVersion` parameter in any handler signature | verified by source-grep |

### 7.2 Endpoint behaviour

| Coverage | Required |
|---|---|
| `POST /links` 201 + Location | happy path; body matches `LinkView` |
| `POST /links` with `Idempotency-Key` first call | 201; `IdempotencyRecord` persisted |
| `POST /links` with `Idempotency-Key` replay | 200 (not 201); body byte-equivalent to the original 201 body; `Location` header still present |
| `POST /links` with `Idempotency-Key` and different body | 409 with ProblemDetails (`type` reflects `Error.Conflict`) |
| `POST /links` with `customShortCode` collision | 409 with ProblemDetails |
| `POST /links` body invalid | 422 with ProblemDetails; `errors` field names the offending field |
| `GET /links?page=1&pageSize=50` | 200; body has `items`, `page`, `pageSize`, `totalCount`; `Link` header present and well-formed |
| `GET /links?pageSize=200` | 400 with ProblemDetails |
| `GET /links?page=0` | 400 with ProblemDetails |
| `GET /links/{id}` own | 200 |
| `GET /links/{id}` another's | 404 (not 403) — existence-leak rule |
| `GET /links/{id}` admin viewing another's | 200 |
| `GET /links/{id}` unknown | 404 |
| `PUT /links/{id}` toggle and extend | 200 with updated `LinkView` |
| `PUT /links/{id}` invalid expiry | 422 |
| `DELETE /links/{id}` | 204; subsequent `GET` returns 404 |
| `GET /links/{id}/stats` happy path | 200 with ETag and `Cache-Control` |
| `GET /links/{id}/stats` with `If-None-Match` matching | 304 with empty body |
| `GET /{shortCode}` eligible | 302 with `Location: <originalUrl>`; `Cache-Control: no-store`; click row persisted |
| `GET /{shortCode}` disabled | 410 with ProblemDetails; **no** click row persisted |
| `GET /{shortCode}` expired | 410; no click row |
| `GET /{shortCode}` unknown | 404; no click row |
| `GET /{shortCode}` is anonymous | request with no `Authorization` header succeeds; the framework does not 401 it |
| `GET /health` | 200; anonymous |

### 7.3 Authentication and authorisation

| Coverage | Required |
|---|---|
| Anonymous request to a `links:*` endpoint | 401 with `Error.AuthenticationRequired` ProblemDetails |
| Authenticated, missing permission | 403 with `Error.Forbidden` ProblemDetails |
| Existence-leak protection (`GET /links/{id}`) | non-owner with `links:read` but no `links:admin` gets 404, not 403 |
| Auth composition end-to-end | the single `IActorProvider` registration produces `Maybe<Actor>.None` for anonymous requests and the bearer-derived actor for authenticated requests |

## 8. ProblemDetails wrapping (`Api/tests`)

| Coverage | Required |
|---|---|
| 400 (validation) | RFC 7807 shape; `errors` collection populated |
| 401 (unauthenticated) | RFC 7807 shape; `WWW-Authenticate` header present per Trellis convention |
| 403 (forbidden) | RFC 7807 shape |
| 404 (not found) | RFC 7807 shape |
| 409 (conflict — custom code collision) | RFC 7807 shape |
| 409 (conflict — idempotency-key mismatch) | RFC 7807 shape; `detail` references the mismatched fields |
| 410 (gone) | RFC 7807 shape; status correctly 410 (not 404, not 200) |
| 422 (invalid input) | RFC 7807 shape; `errors` collection populated |
| 503 (short-code generation exhausted) | RFC 7807 shape; `Retry-After` header optional |

## 9. Round-trip persistence (`Acl/tests`)

For every aggregate root and every owned VO:

| Coverage | Required |
|---|---|
| `Link` insert + reload | every property (`ExpiresAt` absent + present, `IsActive`, `OwnerId`, `OriginalUrl`, `ShortCode`, `CreatedAt`) survives a save + reload |
| `Click` insert + reload | every property (`UserAgent` absent + present, `RefererHost` absent + present, `ClickedAt`) round-trips |
| `IdempotencyRecord` insert + reload | composite `(OwnerId, IdempotencyKey)` round-trips; `LinkId` reference is FK-valid |
| `Maybe<T>` absent | `null` column persists as `Maybe<T>.None`; reloads as `None` |
| `Maybe<T>` present | persists and reloads to a `Some` with equal value |
| Composite unique index on `IdempotencyRecords(OwnerId, IdempotencyKey)` | second insert of same pair fails with EF Core unique-constraint violation; different pair succeeds |
| Unique index on `Links(ShortCode)` | second insert of same `ShortCode` fails with EF Core unique-constraint violation |
| Cascade `Link → Click` | deleting a `Link` deletes its `Click` rows |
| Index `Links(OwnerId, CreatedAt DESC)` | declared in the model; verified via `Model` inspection or schema dump |
| Index `Clicks(LinkId, ClickedAt)` | declared in the model; verified via `Model` inspection or schema dump |

## 10. Stop criteria

The implementation is "test-complete" when every row in §1–§9 has at least one matching assertion. Stopping earlier (e.g., "representative happy + key failure paths") is not sufficient and is explicitly deprecated for eval runs (see `eval-runs/README.md` § Eval Hygiene if a runs folder exists for this lab).
