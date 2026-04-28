# Test Coverage Checklist — Order Returns (Step 8 Delta)

Companion to `coverage-checklist.md`. This file enumerates the **additional** rows that become required once Step 8 (Order Returns) is implemented. The base checklist (`coverage-checklist.md`) covers the v1 16 endpoints and must be green before this delta applies.

Each row should be a separate test (or a single parameterised test with named cases). A row is **green** when the implementation has at least the listed positive and negative assertions.

## R1. Scalar value object — `ReturnReason` (`Domain/tests`)

| Coverage | Required |
|---|---|
| `TryCreate` happy path | a 10–500 character string returns `Result.Ok` and round-trips |
| `TryCreate` boundary low | exactly 10 characters returns `Result.Ok` |
| `TryCreate` boundary high | exactly 500 characters returns `Result.Ok` |
| `TryCreate` below low | 9 characters returns `Result.Fail` with `Error.UnprocessableContent.ForField(...)` |
| `TryCreate` above high | 501 characters returns `Result.Fail` |
| `TryCreate` null/empty/whitespace | `Result.Fail` |
| Equality and `GetHashCode` | identical inputs are equal; differing inputs are not |

## R2. State machine — `Delivered → Returned` (`Domain/tests`)

| Coverage | Required |
|---|---|
| Happy path | Delivered + valid window → `Result<Order>.Ok`; status becomes `Returned`; `ReturnedAt` set; `ReturnReason` set |
| Side-effect verified | reserved stock is released for every line item (same pattern as cancel) |
| Domain event raised | `OrderReturnedEvent(OrderId, CustomerId, ReturnReason, ReturnedAt)` is appended |
| Wrong-source-status (Draft/Submitted/Approved/Shipped/Cancelled/Returned) | each returns `Result.Fail` with `Error.UnprocessableContent`; no state mutation |
| Return window expired | `now - DeliveredAt > TimeSpan.FromDays(30)` returns `Result.Fail`; no state mutation. Both timestamps from injected `TimeProvider` |
| Return window boundary | `now - DeliveredAt == TimeSpan.FromDays(30)` returns `Result.Ok` (inclusive) |
| Idempotency | returning an already-Returned order returns `Result.Fail` |

## R3. Order aggregate additions (`Domain/tests`)

| Coverage | Required |
|---|---|
| `ReturnedAt` is `Maybe<DateTime>` | absent before return, present after; round-trips through persistence |
| `ReturnReason` is a property on Order | absent before return, present after; included in API response |

## R4. Application handler — `ReturnOrderCommand` (`Application/tests`)

| Coverage | Required |
|---|---|
| Happy path | `Result.Ok` with the returned aggregate |
| Missing permission `orders:return` | `Result.Fail` with `Error.Forbidden` |
| Resource-level forbidden | non-owner non-admin returns `Result.Fail` with `Error.Forbidden`; owner succeeds; admin succeeds (mirrors Cancel) |
| Resource not found | `Result.Fail` with `Error.NotFound` |
| Domain failure surfaced | `Result.Fail` from window check / wrong status propagates with the same `Error` |
| `TimeProvider` injected | handler depends on `TimeProvider`, not `DateTime.UtcNow`; tests override time to exercise the 30-day window |
| Save called on success path only | `SaveChangesResultAsync` invoked iff handler succeeded |

## R5. API endpoint — `POST /api/orders/{id}/return` (`Api/tests`)

| Coverage | Required |
|---|---|
| Happy path | 200 OK with updated Order Response (`status: "Returned"`, `returnReason`, `returnedAt` populated) |
| Validation error (window expired, wrong status) | 422 with `Error.UnprocessableContent` body |
| Resource not found | 404 |
| Forbidden — missing `orders:return` permission | 403 |
| Forbidden — resource-level (not owner, not admin) | 403 |
| `api-version` query parameter required | 400 (framework-level) |

## R6. Persistence delta (`Acl/tests`)

| Coverage | Required |
|---|---|
| `DeliveredAt` round-trip (already required by base checklist; re-verify after Returned status added) | absent and present cases both round-trip |
| `ReturnedAt` round-trip | absent and present cases both round-trip |
| `ReturnReason` round-trip | absent and present cases both round-trip |
| `OrderStatus.Returned` persisted as string | reloads to the same enum value |

## Stop criteria

The Step 8 delta is "test-complete" when every row in §R1–§R6 has at least one matching assertion **in addition to** every row in `coverage-checklist.md` still being green.
