# Test Coverage Checklist — URL Shortener (alpha.557 course)

Companion to `specs/url-shortener.md`. This checklist makes the expected test coverage explicit and machine-checkable so models stop at "rubric coverage" rather than "representative happy + key failure paths."

Each row should be a separate test (or a single parameterised test with named cases). A row is **green** when the implementation has at least the listed **positive** and **negative** assertions.

Where it differs from the OM lab's rubric (in [`docs/training-lab.md`](../docs/training-lab.md)): there are dedicated sections for the unversioned-host contract (§7.1), the redirect endpoint and its anonymous-access shape (§7.2), idempotency on POST (§4 and §5), and ETag round-trip (§6). The OM lab's API-versioning checks (e.g., "request without `?api-version=` returns 400") have no equivalent here — by design.

Where it differs from `coverage-checklist-subscription-reminder.md` (the worker checklist): there is no §5 gateway-error mapping, no §4 worker-tick orchestration, no §3.3 counter-invariant. This service is HTTP-shaped end-to-end.

## Required eval minimum (subset)

The L4 (Tests) score is graded against this minimum subset. Rows outside the minimum are extended completeness — required for "test-complete" but not individually scored.

| § | Minimum row |
|---|---|
| §1 | Every scalar VO: valid construction and each applicable domain bound. Opt into required/non-default rules explicitly; do not assume all scalar bases reject empty/default values. |
| §1 | Reused Trellis built-ins integrate cleanly into `Link` — invalid input rejected at aggregate construction |
| §2 | `Link.Disable` / `Link.Enable` idempotency — second call is no-op success, **no domain event raised** the second time |
| §2 | `Link.Extend` rejects new `ExpiresAt` ≤ current `ExpiresAt` |
| §3 | `Link`: `ExpiresAt <= CreatedAt` rejected; `OriginalUrl` scheme non-http(s) rejected |
| §3 | `Link`: `OwnerId`, `ShortCode`, `OriginalUrl` immutable after creation (verified by reflection or by absence of public setters) |
| §4 | Auto/custom creation, collisions, same-key replay/current view and different-request `Error.Conflict.ForReason("idempotency-key-mismatch")`; deleting the original leaves the key and matching replay returns 404. |
| §4 | Link reads/mutations dispatch through actor/resource-aware bases, shared loader and HideExistence (§3.5); permitted non-owner → stable public NotFound, no double load. |
| §4 | `RedirectQuery`: disabled link → `Gone`; expired link → `Gone`; eligible → `Redirect(originalUrl)`; unknown → `NotFound` |
| §4 | `RecordClickCommand`: failure does not bubble out of the redirect orchestrator (the response status is unchanged) |
| §6 | `(OwnerId, IdempotencyKey)` storage-layer unique constraint produces a unique-violation that the handler catches and translates into a replay (200) for matching canonical bodies, or a 409 for mismatched canonical bodies — never a 500 |
| §7 | **Unversioned-host contract:** *both* `Program.cs` and `Api/src/DependencyInjection.cs` are free of `AddApiVersioning(...)`; no route has `?api-version=` requirement; every endpoint reachable without that query parameter; no route template contains `:apiVersion` |
| §7 | **201 Location:** common CreatedAtRoute emits an unversioned URL that dereferences to the created link; no optional versioning SDK. |
| §7 | **200 Location:** common WithLocation emits an unversioned URL that dereferences to the disabled link. |
| §7 | **`HttpContext.PageUrl` test:** `GET /links` paginated response's `Link: <...>; rel="next"` header value contains no `api-version=` query parameter; the implementation calls `HttpContext.PageUrl(` at least once in the list handler (verified by source-grep — the URL must come from the framework helper, not hand concatenation) |
| §7 | `GET /links/{id}` returns 404 (not 403) when the caller is not the owner and not an admin |
| §7 | `GET /{shortCode}`: 302 with `Location` for eligible link, 410 for disabled, 410 for expired, 404 for unknown — all anonymous (no `Authorization` header) |
| §7 | `GET /links/{id}/stats`: second request with `If-None-Match: <prior etag>` returns 304 with empty body; intervening click changes the ETag; a non-owner request with `If-None-Match: <ETag obtained out-of-band>` returns 404, NOT 304 (existence-leak protection on the cached endpoint) |
| §7 | A click row is persisted on 302 redirect; **no** click row is persisted on 410 or 404 |
| §9 | `Links(ShortCode)` unique constraint exercised against real SQLite; second insert raises EF Core unique-violation |

## Extended completeness

Everything below is required for "test-complete" but not individually scored by L4.

## 1. Scalar value objects (`Domain/tests`)

For every scalar VO declared in the spec — `ShortCode`, `OriginalUrl`, `IdempotencyKey`, `UserAgent`, `RefererHost`, and the strongly-typed identity types (`LinkId`, `OwnerId`, `ClickId`):

Apply length/format rows only to types with those rules. RequiredString/Guid/scalar bases
are lenient by default: select `[RequiredTrim]`, `[StringLength]`, `[NotDefault]` or other
documented attributes when the domain requires them. Inherited factories/equality/converters
must not be redeclared. Null for a required reference is tested at its owning input boundary.

| Coverage | Required |
|---|---|
| `TryCreate` happy path | ≥1 valid input returns `Result.Ok` and round-trips |
| `TryCreate` boundary low | minimum-length input returns `Result.Ok` |
| `TryCreate` boundary high | maximum-length input returns `Result.Ok` |
| `TryCreate` below low | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` above high | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` null/empty/whitespace | `Result.Fail` |
| Format / pattern violation | `Result.Fail`. `ShortCode` rejects characters outside `[A-Za-z0-9_-]`. `OriginalUrl` rejects non-http(s) schemes (`ftp://...`, `javascript:...`, relative URLs). `RefererHost` rejects strings that fail DNS-hostname format. `UserAgent` and `IdempotencyKey` are opaque — no format row required beyond length. |
| Reserved-route short code | links/health and case variants fail with `Error.InvalidInput.ForField("short-code.reserved", "shortCode", detail: "...")`; they would collide with root routes. |
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
| `IdempotencyRecord`: `CanonicalRequestJson` required | construction requires a non-null, non-empty canonical-JSON string; missing fails |

## 3.5 Checked actor/resource handlers + HideExistence (`Application/tests` + `Api/tests`)

Spec §6.0 requires real pipeline authorization and shipped actor/resource-aware handler
bases. Test by Mediator dispatch with fake dependencies, not an explicit actor/resource
invocation seam. Keep static operation permissions; admins bypass ownership, not base permissions.

| Coverage | Required |
|---|---|
| **`IAuthorizeResource<Link>` on LinkId-scoped messages** | `GetLinkByIdQuery`, `DisableLinkCommand`, `ExtendLinkExpiryCommand`, `DeleteLinkCommand`, `GetLinkStatsQuery` each declare `: IAuthorizeResource<Link>` and supply an `Authorize(Actor, Link)` body whose ownership-check returns `Result.Fail(new Error.Forbidden("links.not-owner-or-admin", ResourceRef.For<Link>(link.Id.Value)))` for non-owner non-admin and `Result.Ok()` otherwise. Verified by reflection over the message type for the interface marker, plus a direct call to `Authorize(...)` with a non-matching actor. |
| **`IIdentifyResource<Link, LinkId>` on LinkId-scoped messages** | Same five messages also declare `: IIdentifyResource<Link, LinkId>` and project to the `LinkId` on the message. Verified by reflection. |
| **Shared loader** | `LinkResourceLoader : SharedResourceLoaderById<Link, LinkId>` is registered in `Acl/src/DependencyInjection.cs`; the loader returns `Result.Ok(link)` when the EF `FindAsync` yields a value and `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))` when it does not. Verified by direct unit test against an in-memory `DbContext`. |
| **`HideExistence<Link>()` configured** | Configure `services.AddResourceAuthorization(o => o.HideExistence<Link>())` or the matching builder slot. Prove its effect through real dispatch/HTTP tests for missing and withheld links; do not call the internal options resolver. |
| **HideExistence end-to-end** | Missing/withheld responses match stable public status/kind/code/resource/detail; exclude request-specific instance/traceId. Denied dispatch never executes the business hook. |
| **Actor/resource mutation bases** | Disable/extend/delete handlers override protected Handle with the checked actor and exact loaded Link; business-only constructors. Loader invoked once, no fallback repository reload. |
| **Actor snapshot** | A changing provider resolves once per dispatch; static/resource checks and business hook receive the same instance, including permission state. |
| **Deny-aware admin** | Explicitly forbidden links:admin overrides a grant; HasPermission determines the ownership bypass. |
| **Authorize delegation, no manual fallthrough** | Denied Mediator dispatch does not execute the business hook or mutate the loaded Link. Ownership policy lives on the message and uses deny-aware `HasPermission`; source inspection is supplementary, not proof that authorization ran. |

## 4. Command and query handlers (`Application/tests`)

For `CreateLinkCommand`:

| Coverage | Required |
|---|---|
| Happy path — auto-generated code | `Link` persisted; `CreateLinkOutcome.Created(link)`; `ShortCode` matches generator constraints (length, charset) |
| Happy path — custom code | persisted; `CreateLinkOutcome.Created(link)` with the requested `ShortCode` |
| Custom code already taken | `Error.Conflict.ForReason("short-code-taken")`; neither new row persists |
| Auto-code collision and regeneration | fake generator returns a colliding code N times then a fresh one; final outcome is `Created`; exactly one `Link` row persisted |
| Auto-code regeneration exhausted | `new Error.Unavailable { Code = "short-code-generation-exhausted" }` after five total save attempts; no orphan rows |
| Idempotency-key first observation | `IdempotencyRecord` (with `CanonicalRequestJson` populated) and `Link` both persisted in one transaction; outcome `Created` |
| Idempotency-key replay (same canonical body) | second call returns `Replayed(link)` with the same `LinkId`; no new `Link` or `IdempotencyRecord` row; no new domain event raised |
| Idempotency-key replay (reordered JSON keys, same intent) | canonical serialization makes the two requests compare equal; outcome `Replayed(link)` with same `LinkId`; no mutation |
| Idempotency-key reuse with different canonical body | `Error.Conflict.ForReason("idempotency-key-mismatch")`; no mutation, including after deletion |
| Replay after mutation | same original request returns the current disabled/extended LinkView, not a historical creation snapshot |
| Replay after deletion | retained record + matching request → NotFound; no new Link; new key → Created |
| Idempotency-key from a **different owner** with the same key | succeeds; produces a distinct `Link` and a distinct `IdempotencyRecord` |
| No idempotency-key, repeated identical request | each call produces a distinct `Link` (auto-generated codes differ) |
| Transactional atomicity | simulate a database failure between adding the `Link` and `IdempotencyRecord` and committing; assert neither row persists |

For `ListMyLinksQuery`:

| Coverage | Required |
|---|---|
| Owner-scoped filter | returns only the caller's links; admin sees all |
| Pagination | PageRequest honored; ASP emits PagedResponse with next.cursor/href when more rows exist |
| Ordering | typed descending CreatedAt/Id seek; tied timestamps have no duplicates/gaps |
| `limit` out of range | 422 page-size.out-of-range on query limit, using Reject |
| `cursor` that does not decode | 422 cursor.malformed on query cursor |

For `GetLinkByIdQuery`:

| Coverage | Required |
|---|---|
| Own link | 200 with `LinkView` |
| Another's link, no `links:admin` | `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))` — **not** `Error.Forbidden` |
| Another's link, with `links:admin` | 200 with `LinkView` |
| Unknown id | `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))` |

For `DisableLinkCommand`:

| Coverage | Required |
|---|---|
| Disable active link | persisted; `LinkDisabledDomainEvent` raised; `IsActive` round-trips to false |
| Disable already-disabled link | no-op success; **no event raised**; current `LinkView` returned |
| Non-owner disable | `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))` (not `Forbidden`); link state unchanged |
| Admin disable on another's link | succeeds |

For `ExtendLinkExpiryCommand`:

| Coverage | Required |
|---|---|
| Extend with valid future value | `LinkExpiryExtendedDomainEvent` raised; `ExpiresAt` updated |
| Extend with value ≤ current `ExpiresAt` | located InvalidInput on expiresAt; no mutation |
| Extend with value ≤ now | located InvalidInput on expiresAt; no mutation |
| Non-owner extend | `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))`; no mutation |
| Admin extend on another's link | succeeds |

For `DeleteLinkCommand`:

| Coverage | Required |
|---|---|
| Own link | deleted; subsequent `GetLinkByIdQuery` returns `new Error.NotFound(ResourceRef.For<Link>(id.Value))` |
| Cascade to clicks | a `Link` with N clicks deletes both the link row and all N click rows |
| Retain key tombstones | Delete Link/clicks but retain owner/key/canonical request/original LinkId. Same request/key → 404; different request/key → 409; new key → fresh 201. |
| Non-owner delete | `Result.Fail(new Error.NotFound(ResourceRef.For<Link>(id.Value)))`; link still exists |
| Admin delete on another's link | succeeds |

For `GetLinkStatsQuery`:

| Coverage | Required |
|---|---|
| Zero clicks | `TotalClicks = 0`; `FirstClickAt = None`; `LastClickAt = None` |
| Multiple clicks | `TotalClicks` correct; `FirstClickAt = MIN`; `LastClickAt = MAX` |
| Single click | `FirstClickAt == LastClickAt` |
| Non-owner | `new Error.NotFound(ResourceRef.For<Link>(id.Value))` (existence-leak rule); the projection query is **not** executed (verified by repository spy / hit counter) |
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

Authorisation matrix (run as a parameterised test per handler). The "Anonymous" column reflects HTTP-layer behavior: a request with no bearer / no `X-Test-Actor` header. (The framework's `DevelopmentActorProvider` may still produce a default actor for such requests; that's a provider implementation detail. The HTTP outcomes below are what the lab measures.)

| Handler | Required permission | Anonymous (no auth header) | Authenticated, lacks permission | Has permission |
|---|---|---|---|---|
| `CreateLinkCommand` | `links:create` | 401 | 403 | 200/201 |
| `ListMyLinksQuery` | `links:read` | 401 | 403 | 200 |
| `GetLinkByIdQuery` | `links:read` | 401 | 403 (no permission) or 404 (has permission but not owner+not admin) | 200 |
| `DisableLinkCommand` | `links:write` | 401 | 403 (no permission) or 404 (has permission but not owner+not admin) | 200 |
| `ExtendLinkExpiryCommand` | `links:write` | 401 | 403 / 404 | 200 |
| `DeleteLinkCommand` | `links:write` | 401 | 403 / 404 | 204 |
| `GetLinkStatsQuery` | `links:read` | 401 | 403 / 404 | 200 |
| `RedirectQuery` (via `/{shortCode}`) | none | 302 / 410 / 404 | 302 / 410 / 404 | 302 / 410 / 404 |
| Health | none | 200 | 200 | 200 |

## 5. Idempotency (`Application/tests` + `Acl/tests`)

| Coverage | Required |
|---|---|
| Storage-layer composite unique | ACL atomic save returns classified duplicate-key conflict; fresh read resolves replay/current view, mismatch 409 or deleted-target 404 |
| Cross-owner key reuse | same key from a different `OwnerId` succeeds; produces a distinct `IdempotencyRecord` |
| Canonical-body equivalence | reordered keys replay; changed value yields Error.Conflict.ForReason("idempotency-key-mismatch") |
| `CanonicalRequestJson` persisted | the value stored on the original `201` record is exactly the canonical serialization of the three input fields; round-trips through `Acl/tests` |
| Transactional all-or-nothing | a forced failure between adding the entities and committing rolls back both; a subsequent retry with the same key succeeds as a fresh first-observation |
| Concurrent first-attempt race | separate contexts, same owner/key/request → one Created, one Replayed, same LinkId, no orphan Link |
| Helper boundaries | never two independently committed TryInsertUniqueAsync calls; Link+key remain atomic, and FK/infrastructure/cancellation faults are not replay successes |

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
| Authorization precedes ETag | a non-owner without `links:admin` requesting `/links/{id}/stats` with a valid `If-None-Match` for that link's ETag receives **404**, not 304 — the ownership check runs before the ETag comparison so existence is never leaked to an unauthorized caller |

## 7. HTTP endpoints (`Api/tests`)

### 7.1 Unversioned-host contract (the lab's central test)

| Coverage | Required |
|---|---|
| Composition is unversioned | No API-versioning package, registration, versioned URL policy, or route segment. Verify endpoint metadata, bare-URL requests and generated links; do not reference optional SDK interfaces solely to assert their absence. |
| No `:apiVersion` route segment | no controller, minimal-API route, or attribute-routed action template contains the literal `:apiVersion`; verified by source-grep |
| No `?api-version=` requirement | every route is reachable with the bare URL — `GET /links`, `POST /links`, `GET /links/{id}`, `POST /links/{id}/disable`, `PUT /links/{id}/expiry`, `DELETE /links/{id}`, `GET /links/{id}/stats`, `GET /{shortCode}`, `GET /health` |
| `?api-version=1.0` appended to any route | ignored like an unrecognized query parameter; the endpoint's normal response is unchanged, with no version-reader 400 |
| Common CreatedAtRoute | 201 Location has no version parameter and dereferences; no optional versioning package |
| Common WithLocation | 200 Location has no version parameter and dereferences to current state |
| Common PageUrl | GET /links?limit=2 produces unversioned next.href/Link URLs; following them covers every item once |
| ProblemDetails type URIs | `type` field on error responses does not reference an api-version-specific path |
| No `ApiVersion` parameter in any handler signature | verified by source-grep |

### 7.2 Endpoint behaviour

| Coverage | Required |
|---|---|
| `POST /links` 201 + Location | happy path; body matches `LinkView` |
| `POST /links` with `Idempotency-Key` first call | 201; `IdempotencyRecord` persisted with `CanonicalRequestJson` |
| `POST /links` with `Idempotency-Key` replay | 200/current LinkView with Location if live; 404 if deleted, without recreation |
| `POST /links` with `Idempotency-Key` replay (reordered keys, same intent) | 200; canonical-JSON comparison treats reordered request as equivalent |
| `POST /links` with changed canonical request | 409 ProblemDetails with kind conflict and code idempotency-key-mismatch |
| `POST /links` with `customShortCode` collision | 409 with ProblemDetails |
| `POST /links` body invalid | 422 with fieldViolations and body locations |
| `GET /links?limit=50` | PagedResponse body and next Link when needed; absent on last page |
| Invalid raw pagination | empty/whitespace/repeated/malformed cursor → 422 cursor.malformed; empty/malformed/overflow/repeated limit → 422 format.integer; nonpositive or above 100 → 422 page-size.out-of-range; assert query location/name |
| OpenAPI pagination | cursor/limit declared separately, with no bound parameters that intercept/normalize raw input |
| `GET /links/{id}` own | 200 |
| `GET /links/{id}` another's | 404 (not 403) — existence-leak rule |
| `GET /links/{id}` admin viewing another's | 200 |
| `GET /links/{id}` unknown | 404 |
| `POST /links/{id}/disable` own active link | 200 with updated `LinkView`; `Location: /links/{id}` |
| `POST /links/{id}/disable` own already-disabled link | 200; no domain event raised; `Location` still present |
| `POST /links/{id}/disable` another's link (no admin) | 404 |
| `PUT /links/{id}/expiry` valid extension | 200 with updated `LinkView` |
| `PUT /links/{id}/expiry` invalid expiry (≤ current) | 422 |
| `PUT /links/{id}/expiry` another's link (no admin) | 404 |
| `DELETE /links/{id}` | 204; subsequent `GET` returns 404 |
| `GET /links/{id}/stats` happy path | 200 with ETag and `Cache-Control` |
| `GET /links/{id}/stats` with `If-None-Match` matching | 304 with empty body |
| `GET /links/{id}/stats` non-owner with `If-None-Match` matching | 404 (existence-leak rule on the cached endpoint), NOT 304 |
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
| Auth composition end-to-end | the single `IActorProvider` registration yields the bearer-derived actor for authenticated requests; anonymous requests to `links:*` endpoints result in 401 at the HTTP layer (regardless of what the framework's default `DevelopmentActorProvider` returns under the hood) |

## 8. ProblemDetails wrapping (`Api/tests`)

| Coverage | Required |
|---|---|
| 400 (malformed JSON/protocol) | RFC 9457 application/problem+json, not domain-validation 400 |
| 401 (unauthenticated) | RFC 9457; configured authentication scheme supplies its challenge |
| 403 (forbidden) | RFC 9457, kind forbidden |
| 404 (not found) | RFC 9457; stable public hidden/missing fields |
| 409 (custom code collision) | RFC 9457, code short-code-taken |
| 409 (key mismatch) | RFC 9457, code idempotency-key-mismatch; no detail-string parsing |
| 410 (gone) | RFC 9457, status 410 |
| 422 (invalid input) | RFC 9457; fieldViolations/ruleViolations with code and location(s) |
| 503 (code generation exhausted) | RFC 9457, code short-code-generation-exhausted |

## 9. Round-trip persistence (`Acl/tests`)

For every aggregate root and every owned VO:

| Coverage | Required |
|---|---|
| `Link` insert + reload | every property (`ExpiresAt` absent + present, `IsActive`, `OwnerId`, `OriginalUrl`, `ShortCode`, `CreatedAt`) survives a save + reload |
| `Click` insert + reload | every property (`UserAgent` absent + present, `RefererHost` absent + present, `ClickedAt`) round-trips |
| `IdempotencyRecord` round-trip | owner/key/canonical request/original LinkId retained, including after Link deletion; no mandatory live-Link FK |
| `Maybe<T>` absent | `null` column persists as `Maybe<T>.None`; reloads as `None` |
| `Maybe<T>` present | persists and reloads to a `Some` with equal value |
| Composite unique index on `IdempotencyRecords(OwnerId, IdempotencyKey)` | second insert of same pair fails with EF Core unique-constraint violation; different pair succeeds |
| Unique index on `Links(ShortCode)` | second insert of same `ShortCode` fails with EF Core unique-constraint violation |
| Cascade `Link → Click` | deleting a `Link` deletes its `Click` rows |
| Retain `IdempotencyRecord` after deletion | Link/clicks removed; record still exists and pins old-key replay to 404 |
| Index `Links(OwnerId, CreatedAt DESC, Id DESC)` | declared in the model |
| Index `Clicks(LinkId, ClickedAt)` | declared in the model; verified via `Model` inspection or schema dump |

## 10. Stop criteria

The implementation is "test-complete" when every row in §1–§9 has at least one matching assertion. Stopping earlier (e.g., "representative happy + key failure paths") is not sufficient and is explicitly deprecated for eval runs (see `eval-runs/README.md` § Eval Hygiene if a runs folder exists for this lab).
