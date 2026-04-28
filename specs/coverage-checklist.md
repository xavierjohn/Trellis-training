# Test Coverage Checklist — Order Management

Companion to `specs/order-management-sqlite.md` (and the cosmosdb variant). The eval rubric (`docs/evaluation-criteria.md` Level 4) scores test consistency, but does not enumerate every case. This checklist makes the expected coverage explicit and machine-checkable so models stop at "rubric coverage" rather than "representative happy + key failure paths."

Each row should be a separate test (or a single parameterised test with named cases). A row is **green** when the implementation has at least the listed **positive** and **negative** assertions.

## 1. Scalar value objects (`Domain/tests`)

For every scalar VO declared in the spec — `FirstName`, `LastName`, `EmailAddress`, `PhoneNumber`, `Sku`, `ProductName`, `Money` (composite), `Quantity`, `ReturnReason` (Step 8), and the strongly-typed identity types (`CustomerId`, `ProductId`, `OrderId`, `LineItemId`):

| Coverage | Required |
|---|---|
| `TryCreate` happy path | ≥1 valid input returns `Result.Ok` and round-trips |
| `TryCreate` boundary low | minimum-length / minimum-value input returns `Result.Ok` |
| `TryCreate` boundary high | maximum-length / maximum-value input returns `Result.Ok` |
| `TryCreate` below low | `Result.Fail` with `Error.UnprocessableContent.ForField(...)` |
| `TryCreate` above high | `Result.Fail` with `Error.UnprocessableContent.ForField(...)` |
| `TryCreate` null/empty/whitespace (where applicable) | `Result.Fail` |
| Format / pattern violation (where applicable — `EmailAddress`, `Sku`, `PhoneNumber`) | `Result.Fail` |
| Equality and `GetHashCode` | two VOs with identical inputs are equal; differing inputs are not equal |

## 2. State machine (`Domain/tests`)

For every transition declared in the spec § State Machine (Draft→Submitted, Submitted→Approved, Approved→Shipped, Shipped→Delivered, Delivered→Returned [Step 8], plus all four routes to Cancelled):

| Coverage | Required |
|---|---|
| Happy path | source status → target status returns `Result<Order>.Ok`; status property is updated; relevant `*At` timestamp set |
| Side-effect verified | stock reservation / release / `*At` timestamp / `ReturnReason` is asserted on the resulting aggregate |
| Domain event raised | `_domainEvents` contains the expected event type with the expected payload |
| Wrong-source-status (every other status) | returns `Result.Fail` with `Error.UnprocessableContent`; no state mutation |
| Precondition violation (where the transition declares one — empty line items for Submit, insufficient stock for Submit, return window expired for Return) | `Result.Fail`; no state mutation |
| Idempotency / repeat-call | calling the same transition again from the target status returns `Result.Fail` (not double-mutating) |

## 3. Aggregate invariants (`Domain/tests`)

| Coverage | Required |
|---|---|
| `AddLineItem` duplicate productId | `Result.Fail` (per spec §3.4 + §6.7); no mutation |
| `AddLineItem` quantity boundary (1, 999, 0, 1000) | low/high boundary `Ok`; below/above `Fail` |
| `RemoveLineItem` non-existent lineItemId | `Result.Fail` with `Error.NotFound` |
| `RemoveLineItem` last remaining line item | `Result.Fail` (per spec §6.8); no mutation |
| `Order.Total()` | sum of `quantity * unitPrice` across line items; mixed currency returns `Result.Fail` |
| Line item snapshot | changing `Product.UnitPrice` after add does not change the existing line item's `UnitPrice` |
| ETag drift | mutating the aggregate changes `ETag` |

## 4. Application handlers (`Application/tests`)

For every Command and Query in spec §6 (Create/Get Customer, Create/Get Product, Add Stock, Create Draft Order, Add/Remove Line Item, Submit/Approve/Ship/Deliver/Cancel Order, Return Order [Step 8], Get Order, List Orders by Customer, List Overdue Orders):

| Coverage | Required |
|---|---|
| Happy path | `Result.Ok` with the expected aggregate / projection |
| Missing permission | handler returns `Result.Fail` with `Error.Forbidden`; resource not loaded if pre-load auth is used |
| Resource not found (where the handler loads by id) | `Result.Fail` with `Error.NotFound` |
| Resource-level forbidden (only `CancelOrderCommand` and `ReturnOrderCommand`) | non-owner non-admin returns `Result.Fail` with `Error.Forbidden`; owner succeeds; admin succeeds |
| Domain failure surfaced | a domain `Result.Fail` (e.g., wrong status) propagates as the handler's `Result.Fail` with the same `Error` |
| Repository called with correct args | verify via fake repository assertions |
| Save called on success path only | `SaveChangesResultAsync` invoked iff handler succeeded |

## 5. API endpoints (`Api/tests`)

For every endpoint in spec §7 (16 endpoints in v1 + 1 endpoint added in Step 8):

| Coverage | Required |
|---|---|
| Happy path | expected status (200/201) + Problem Details body or response body shape per §7.1 |
| `201 Created` includes `Location` header pointing at GetById (where applicable) |
| Validation error | 422 with `Error.UnprocessableContent` body (NOT 400) |
| Resource not found | 404 |
| Forbidden (missing permission OR resource-level not owner) | 403 |
| Conflict (duplicate email / SKU) | 409 |
| `api-version` query parameter required | request without it returns 400 (framework-level) |
| ETag round-trip (single-resource GET) | response has `ETag` header; subsequent `If-None-Match` returns 304 |

## 6. Specifications (`Acl/tests`)

For every `Specification<T>` declared (today: `OverdueOrderSpecification`):

| Coverage | Required |
|---|---|
| In-memory `IsSatisfiedBy` | true for matching aggregate; false for non-matching |
| EF translation via `Where(spec)` | hits SQLite/Cosmos in an integration test; returns the same row count as in-memory evaluation over the same data |
| EF translation via `Where(spec.ToExpression())` | same as above (covers `MaybeQueryInterceptor` rewriting of `Maybe<T>` member access in the expression tree) |

## 7. Round-trip persistence (`Acl/tests`)

For every aggregate root and every owned entity / value object:

| Coverage | Required |
|---|---|
| Insert + reload | every property (including `Maybe<T>` absent, `Maybe<T>` present, owned VOs, owned-collection items) survives a save + reload |
| `OwnsMany` collection | inserted line items reload non-empty; order preserved |
| Maybe absent | `null` column persists as `Maybe<T>.None`; round-trips back to `None` |
| Maybe present | persists and reloads to a `Some` with equal value |
| `RequiredEnum<T>` | persists as the declared field name; reloads to the same enum value |
| `[OwnedEntity]` / `Money` (composite VO) | column-per-component, both round-trip |

## 8. Stop criteria

The implementation is "test-complete" when every row in §1–§7 has at least one matching assertion. Stopping earlier (e.g., "representative happy + key failure paths") is not sufficient and is explicitly deprecated for eval runs (see `eval-runs/README.md` § Eval Hygiene).
