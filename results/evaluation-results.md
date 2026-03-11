# Trellis AI Evaluation Results

Tracks how well different AI models implement the Order Management spec using Trellis conventions.

**Evaluation spec:** Order Management (see [`specs/order-management.md`](../specs/order-management.md))
**Scoring framework:** 57 criteria across 5 levels (see [`docs/training-lab.md`](../docs/training-lab.md))
**Goal:** Total score of 52+/57
**Trellis version:** 3.0.0-alpha.106
**Template version:** Trellis.AspTemplate 1.0.3-alpha

---

## Summary Table

| Date | AI Model | Build | Tests | L1 (/18) | L2 (/13) | L3 (/13) | L4 (/9) | L5 (/4) | Total (/57) | Verdict |
|------|----------|-------|-------|----------|----------|----------|---------|---------|-------------|---------|
| 2025-07-10 | Claude Opus 4.6 (Copilot) | 0 errors | 74/75 | 18/18 | 13/13 | 13/13 | 5/9 | 4/4 | **53/57** | **PASS** |
| 2026-03-10 | GPT-5.4 (Copilot) | 0 errors | 3/3 | 17/18 | 11/13 | 12/13 | 1/9 | 4/4 | **45/57** | **FAIL** |
| 2026-03-10 | Claude Sonnet 4.6 (Copilot) | 0 errors | 127/127 | 18/18 | 13/13 | 13/13 | 7/9 | 4/4 | **55/57** | **PASS** |

---

## Detailed Scorecard: Claude Opus 4.6 (Copilot)

**Date:** 2025-07-10
**Model:** Claude Opus 4.6 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 74/75 (1 failure — `Full_order_lifecycle_happy_path` test isolation bug: hardcoded email)
**Test breakdown:** 50 Domain + 0 Application + 12 API integration + 13 template = 75

### Level 1: Structural Consistency — 18/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | CustomerFirstName, CustomerLastName, ProductName, Sku, LineItemQuantity, StockQuantity, ShippingAddress (composite VO), Money (Trellis), all IDs |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\> |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | `StateMachine<string, string>` with lazy init pattern |
| 6 | State transitions return Result | **PASS** | `Machine.FireResult(trigger)` returns Result |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with parameterized DateTime |
| 9 | CQRS pattern used | **PASS** | 11 commands/queries with handlers via Mediator |
| 10 | Authorization on commands | **PASS** | All commands implement IAuthorize; CancelOrderCommand implements IAuthorizeResource\<Order\> |
| 11 | Permissions as constants | **PASS** | 11 constants in Domain Permissions class |
| 12 | Repository interfaces in Application | **PASS** | ICustomerRepository, IProductRepository, IOrderRepository in Application |
| 13 | EF Core in Acl (T) | **PASS** | DbContext and repository implementations in Acl |
| 14 | ApplyTrellisConventions used (T) | **PASS** | In ConfigureConventions; zero HasConversion() calls |
| 15 | Project structure matches template (T) | **PASS** | Correct 4-project structure with correct dependency direction |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain and Application layers |
| 17 | build/test.props shared (T) | **PASS** | Exists; no GlobalUsings.cs in test projects |
| 18 | No primitive obsession | **PASS** | No raw Guid/string/int in domain methods (only in source-generated .g.cs files) |

### Level 2: Behavioral Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `Submit(Func<ProductId, int, Result<Unit>> reserveStock)` — calls reserveStock for each line item, fails on error |
| 2 | Cancel releases stock | **PASS** | `Cancel(Action<ProductId, int>? releaseStock = null)` — optional; Draft cancel doesn't release, Submitted/Approved cancel does |
| 3 | Line item price snapshot | **PASS** | LineItem stores UnitPrice (Money) captured at creation |
| 4 | Duplicate product in order | **PASS** | AddLineItem rejects duplicate product (tested) |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains (tested) |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError used per spec |
| 7 | Order total computed | **PASS** | `CalculateTotal()` uses Money.Multiply + Money.Add; test verifies 3 x $9.99 = .97 |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime, used with `.Where(spec)` on IQueryable |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use RequiredGuid with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer — source generator handles persistence |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler |
| 12 | Cancel resource auth check | **PASS** | CancelOrderCommand: IAuthorizeResource\<Order\> with ownership check (actor == CreatedByActorId OR has orders:read-all) |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories |

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain -> Application -> Acl -> Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present: 3 Customer, 3 Product, 10 Order (16 total including reads) |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + controllers in `v2025_11_12/Controllers/` |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` — integration test confirms 200 OK |
| 11 | DTOs in Api layer | **PASS** | Models in `v2025_11_12/Models/` (CustomerModels.cs, OrderModels.cs, ProductModels.cs) |
| 12 | EF Core entity configurations | **PASS** | 4 IEntityTypeConfiguration classes in `Acl/src/Configurations/` |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 5/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 50 domain tests: Customer (2), Order (22), Product (7), LineItemQuantity (3), Sku (6), StockQuantity (10) |
| 2 | Happy path tests | **PASS** | All operations have at least one success test |
| 3 | Error path tests | **PASS** | Invalid transitions, validation failures, insufficient stock, duplicate product, last item — all tested |
| 4 | State machine tests | **PASS** | Comprehensive: Submit/Approve/Ship/Deliver/Cancel valid + 6 invalid transition tests |
| 5 | Specification test | **FAIL** | No unit test for OverdueOrderSpecification. Integration test hits GET /api/Orders/overdue but doesn't test spec logic directly |
| 6 | Authorization tests | **FAIL** | No permission-denied or resource-auth tests. Application.Tests has DI wiring but zero test methods. No 403 test in API integration tests |
| 7 | Maybe assertion tests | **FAIL** | Tests use `HasValue.Should().BeFalse()` / `.BeTrue()` instead of Trellis.Testing `.Should().HaveValue()` / `.Should().BeNone()` |
| 8 | API integration tests | **FAIL** | 12 integration tests but: (1) missing 403 on missing permission test, (2) `Full_order_lifecycle_happy_path` fails from hardcoded email (test isolation bug) |
| 9 | Trellis.Testing used | **PASS** | `.Should().BeSuccess()` / `.Should().BeFailure()` used extensively throughout domain tests |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | 4 FPs with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | 10 specific Trellis features listed |
| 4 | Copilot instructions feedback | **PASS** | Identifies `ct` -> `cancellationToken` naming issue and Maybe\<T\> HasIndex documentation gap |

### Overall Assessment

**Score: 53/57 (93%) — PASS**

**Strengths:** Perfect L1-L3 (44/44). First-ever 13/13 on L2 — `ParallelAsync` used correctly. Clean architecture, correct patterns, comprehensive domain tests, and high-quality feedback.

**Weaknesses:** L4 at 5/9 — missing specification unit test, no authorization tests, `HasValue.Should().BeTrue()` instead of `Should().HaveValue()`, and one integration test fails from hardcoded email.

**Friction points reported:**
1. CA1725 `ct` naming (Medium) — copilot instruction examples used `ct` but TreatWarningsAsErrors enforces `cancellationToken`
2. Money.Create throws (Low) — inconsistent with errors-as-values principle
3. DomainEvents protected (Low) — can't verify specific events in unit tests
4. HasIndex with Maybe\<T\> (Medium) — string-based backing field reference needed

> FP-1 and FP-4 have been addressed in copilot instructions since this run.

---

## Detailed Scorecard: GPT-5.4 (Copilot)

**Date:** 2026-03-10
**Model:** GPT-5.4 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 3/3 (only 3 API integration tests exist; Domain/Application/ACL test projects are empty shells)
**Test breakdown:** 0 Domain + 0 Application + 3 API integration + 0 template-preserved = 3

### Level 1: Structural Consistency — 17/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | FirstName, LastName, ProductName, Sku, ShippingAddress (composite), Money, all IDs, OrderQuantity, StockQuantity, StockAdditionQuantity, EmailAddress, PhoneNumber, OrderStatus |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\> |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `OrderLineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | `StateMachine<string, string>` with lazy init, string constants for triggers |
| 6 | State transitions return Result | **PASS** | `Machine.FireResult(trigger)` returns Result |
| 7 | Domain events defined | **PASS** | All 5: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent |
| 8 | Specification exists | **PASS** | `OverdueOrdersSpecification : Specification<Order>` with parameterized DateTime |
| 9 | CQRS pattern used | **PASS** | 11 commands + 3 queries with handlers via Mediator |
| 10 | Authorization on commands | **PASS** | All commands implement IAuthorize; CancelOrderCommand implements IAuthorizeResource\<Order\> |
| 11 | Permissions as constants | **PASS** | 11 constants in Domain Permissions class |
| 12 | Repository interfaces in Application | **PASS** | ICustomerRepository, IProductRepository, IOrderRepository in Application |
| 13 | EF Core in Acl (T) | **PASS** | DbContext and repository implementations in Acl |
| 14 | ApplyTrellisConventions used (T) | **PASS** | In ConfigureConventions; zero HasConversion() calls |
| 15 | Project structure matches template (T) | **PASS** | Correct 4-project structure with correct dependency direction |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain and Application layers |
| 17 | build/test.props shared (T) | **PASS** | `build/test.props` exists; no GlobalUsings.cs in test projects |
| 18 | No primitive obsession | **FAIL** | FirstName, LastName, ProductName, Street, City, StateProvince, PostalCode, Country, ActorId all use `ScalarValueObject<T, string>` instead of `RequiredString<T>`. No `[StringLength]` used anywhere. |

### Level 2: Behavioral Consistency — 11/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler fetches products, calls `product.ReserveStock()` for each line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | Handler checks `RequiresStockReleaseOnCancellation()`, releases stock from Submitted/Approved, skips for Draft |
| 3 | Line item price snapshot | **PASS** | UnitPrice (Money) captured at OrderLineItem.TryCreate time from product |
| 4 | Duplicate product in order | **PASS** | Order.TryCreate() validates no duplicate ProductIds; AddLineItem also rejects duplicates |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError, ForbiddenError used correctly |
| 7 | Order total computed | **PASS** | `RecalculateTotal()` sums line items via Money.Multiply + Money.Add |
| 8 | Overdue spec correct | **PASS** | Submitted status + threshold, parameterized DateTime, used with `.Where(spec)` on IQueryable |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use `RequiredGuid<T>` with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer; also `partial Maybe<DateTime>` on Order |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler and AddLineItemHandler |
| 12 | Cancel resource auth check | **FAIL** | Uses `resource.CreatedByActorId.Value == actor.Id` (raw string comparison) and `orders:read-all` (read permission) as admin fallback for cancel |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories consistently |

### Level 3: Architecture & API Consistency — 12/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` |
| 4 | IActorProvider registered | **PASS** | HttpContextActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present: 2 Customer, 2 Product, 10 Order |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + controllers in `v2026_11_12/Controllers/` |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **FAIL** | Uses `MapGet("/health", ...)` instead of `MapHealthChecks("/health")` |
| 11 | DTOs in Api layer | **PASS** | Models in `v2026_11_12/Models/SharedModels.cs` |
| 12 | EF Core entity configurations | **PASS** | 3 IEntityTypeConfiguration classes (Customer, Order, Product) in Acl |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 1/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **FAIL** | Zero domain test classes. Domain.Tests project is an empty shell. |
| 2 | Happy path tests | **FAIL** | No domain tests at all |
| 3 | Error path tests | **FAIL** | No domain tests at all |
| 4 | State machine tests | **FAIL** | No tests |
| 5 | Specification test | **FAIL** | No tests |
| 6 | Authorization tests | **FAIL** | No tests |
| 7 | Maybe assertion tests | **FAIL** | No tests |
| 8 | API integration tests | **FAIL** | Only 3 tests (health, create customer, missing api-version). No lifecycle test, no 403 test. |
| 9 | Trellis.Testing used | **PASS** | `test.props` imports Trellis.Testing; framework is wired up correctly |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md generated in repo root |
| 2 | Friction points specific | **PASS** | 2 FPs with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | Lists Result\<T\> pipelines, typed IDs, AddTrellisBehaviors, EF conventions |
| 4 | Copilot instructions feedback | **PASS** | Identifies `[StringLength]` on `RequiredString<T>` ambiguity — explains fallback to ScalarValueObject |

### Overall Assessment

**Score: 45/57 (79%) — FAIL**

**Strengths:** Near-perfect L1-L3 (40/44). Clean architecture, correct DI, versioning, Problem Details, EnsureCreated. State machine with Stateless + FireResult correct. `ParallelAsync` used in handlers. `SaveChangesResultUnitAsync` consistent. `partial Maybe<T>` properties correct. Composite index on `("Status", "_submittedAt")` uses correct string-based backing field. Pure ROP — zero try/catch. Good feedback. Comprehensive `.http` file.

**Weaknesses:** L4 at 1/9 — GPT-5.4 wrote zero domain tests. All three test projects (Domain, Application, ACL) are empty shells. Only 3 trivial API integration tests exist. Also failed to use `RequiredString<T>` for any string value objects, falling back to manual `ScalarValueObject` validation.

**Friction points reported:**
1. SmartEnum setter API discoverability (Medium) — `RequiredEnum<T>` setter path not obvious for state machine hydration
2. `Maybe<T>` querying (Medium) — easy to misuse in LINQ/specification expressions

**Key insight from feedback:** GPT-5.4 reported that `[StringLength]` on `RequiredString<T>` "was not valid on the class declarations we authored" — this suggests the copilot instructions need a clearer example of `[StringLength]` attribute placement on RequiredString subclasses.

---

## Detailed Scorecard: Claude Sonnet 4.6 (Copilot)

**Date:** 2026-03-10
**Model:** Claude Sonnet 4.6 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 127/127 (all pass)
**Test breakdown:** 42 Domain + 18 Application + 27 API integration + 40 template/ACL = 127

### Level 1: Structural Consistency — 18/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | CustomerFirstName, CustomerLastName, ProductName, Sku, LineItemQuantity, StockQuantity, ShippingAddress (composite VO), Money (Trellis), EmailAddress, PhoneNumber, ActorId, all IDs |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\>; Sku/LineItemQuantity/StockQuantity use ScalarValueObject appropriately for custom validation |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | `StateMachine<OrderStatus, string>` with lazy init via `RequiredEnum<OrderStatus>` — strongly typed states |
| 6 | State transitions return Result | **PASS** | `Machine.FireResult(trigger)` returns Result |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with parameterized DateTime |
| 9 | CQRS pattern used | **PASS** | Commands/queries with handlers via Mediator |
| 10 | Authorization on commands | **PASS** | All commands implement IAuthorize; CancelOrderCommand implements IAuthorizeResource\<Order\> |
| 11 | Permissions as constants | **PASS** | Constants in Domain Permissions class |
| 12 | Repository interfaces in Application | **PASS** | ICustomerRepository, IProductRepository, IOrderRepository in Application |
| 13 | EF Core in Acl (T) | **PASS** | DbContext and repository implementations in Acl |
| 14 | ApplyTrellisConventions used (T) | **PASS** | In ConfigureConventions; zero HasConversion() calls |
| 15 | Project structure matches template (T) | **PASS** | Correct 4-project structure with correct dependency direction |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain and Application layers |
| 17 | build/test.props shared (T) | **PASS** | Exists; no GlobalUsings.cs in test projects |
| 18 | No primitive obsession | **PASS** | CustomerFirstName/CustomerLastName/ProductName use `RequiredString<T>` with `[StringLength]`; ActorId uses `RequiredString<ActorId>` |

### Level 2: Behavioral Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler fetches products, calls `product.ReserveStock()` per line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | Handler checks `RequiresStockRelease()`, releases stock from Submitted/Approved, skips for Draft |
| 3 | Line item price snapshot | **PASS** | LineItem stores UnitPrice (Money) captured at creation |
| 4 | Duplicate product in order | **PASS** | AddLineItem rejects duplicate product (tested) |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains (tested) |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError, ForbiddenError used per spec |
| 7 | Order total computed | **PASS** | `CalculateTotal()` uses Money.Multiply + Money.Add |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use `RequiredGuid<T>` with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer; `partial Maybe<DateTime>` for SubmittedAt, ShippedAt on Order |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler and AddLineItemHandler |
| 12 | Cancel resource auth check | **PASS** | `actor.IsOwner(order.CreatedByActorId.Value)` with `orders:read-all` admin fallback |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories consistently |

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present: Customers, Products (with stock-additions), Orders (10 endpoints including lifecycle + overdue + customer orders) |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + controllers in `v2026_11_12/Controllers/` |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` — proper dedicated method |
| 11 | DTOs in Api layer | **PASS** | Models in `v2026_11_12/Models/` |
| 12 | EF Core entity configurations | **PASS** | 3 IEntityTypeConfiguration classes (Customer, Order, Product) in Acl/Configurations |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 7/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 42 domain tests: Order (23), Product (6), Specification (4), ValueObjects (9 across Sku, LineItemQuantity, StockQuantity, OrderStatus) |
| 2 | Happy path tests | **PASS** | All operations have at least one success test |
| 3 | Error path tests | **PASS** | Invalid transitions, validation failures, insufficient stock, duplicate product, last item — all tested |
| 4 | State machine tests | **PASS** | Submit/Approve/Ship/Deliver/Cancel valid + invalid transitions (Approve from Draft, Cancel from Shipped/Delivered) |
| 5 | Specification test | **PASS** | 4 tests: recently submitted = false, draft order = false, composition with And, overdue order detection |
| 6 | Authorization tests | **PASS** | 18 application tests including: cancel by creator, cancel by admin (read-all), cancel by non-owner → 403, cancel without permission → 403, submit without permission → 403 |
| 7 | Maybe assertion tests | **FAIL** | Uses `HasValue.Should().BeTrue()` instead of Trellis.Testing `.Should().HaveValue()` / `.Should().BeNone()` |
| 8 | API integration tests | **PASS** | 27 API tests: full lifecycle, 403 authorization tests, duplicate 409, validation 400, CRUD, overdue |
| 9 | Trellis.Testing used | **FAIL** | Domain tests use custom `ShouldBeSuccess()`/`ShouldBeFailure()` extensions instead of Trellis.Testing `.Should().BeSuccess()`. Application tests correctly use Trellis.Testing assertions. |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | 9 friction points (FP-1 through FP-9) with detailed category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | 10 specific Trellis features listed including ApplyTrellisConventions, Trellis.Testing, ResourceLoaderById, VersionByNamespaceConvention |
| 4 | Copilot instructions feedback | **PASS** | 5 feedback items on instruction clarity and coverage |

### Overall Assessment

**Score: 55/57 (96%) — PASS**

**Strengths:** Perfect L1-L3 (44/44) — second model to achieve this. `RequiredString<T>` with `[StringLength]` used correctly where GPT-5.4 failed. `RequiredEnum<OrderStatus>` for strongly typed state machine states. `actor.IsOwner()` used instead of raw string comparison. `MapHealthChecks` instead of `MapGet`. Comprehensive test suite: 42 domain + 18 application + 27 API = 87 custom tests all passing. Authorization tests in application layer with proper TestActorProvider impersonation. Specification tested with 4 dedicated tests including composition. 127/127 tests pass with zero failures. Excellent feedback with 9 friction points.

**Weaknesses:** L4 at 7/9 — domain tests use custom `ShouldBeSuccess()`/`ShouldBeFailure()` extensions instead of Trellis.Testing FluentAssertions (though application tests use Trellis.Testing correctly). `HasValue.Should().BeTrue()` used for Maybe assertions instead of `.Should().HaveValue()`. api.http file not updated for OrderManagement endpoints (still has template WeatherForecast endpoints).

**Friction points reported:**
1. Task/ValueTask overload ambiguity (Medium) — `SaveChangesResultUnitAsync` overload resolution with cancellation tokens
2. Money factory pattern (Low) — `Money.Create` vs `Money.TryCreate` inconsistency
3. WhenAllAsync discoverability (Medium) — hard to find without explicit documentation
4. IAuthorize wrapping (Low) — verbose permission array syntax
5. Lazy state machine (Medium) — null-forgiving operator needed for lazy init
6. Resource loader double registration (Medium) — `AddResourceAuthorization` + `AddResourceLoaders` both needed
7. Maybe\<T\> in indexes (Medium) — string-based backing field reference not obvious
8. xUnit parallelism state bleed (Medium) — test isolation with shared DI containers
9. ParallelAsync tuple destructuring (Low) — verbose pattern for multiple parallel results

**Notable improvement over GPT-5.4:** Successfully used `[StringLength]` on `RequiredString<T>` subclasses, comprehensive test coverage across all layers, and proper authorization testing.
