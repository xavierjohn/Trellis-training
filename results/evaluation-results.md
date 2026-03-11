# Trellis AI Evaluation Results

Tracks how well different AI models implement the Order Management spec using Trellis conventions.

**Evaluation spec:** Order Management (see [`specs/order-management.md`](../specs/order-management.md))
**Scoring framework:** 57 criteria across 5 levels (see [`docs/training-lab.md`](../docs/training-lab.md))
**Goal:** Total score of 52+/57
**Trellis version:** 3.0.0-alpha.104
**Template version:** Trellis.AspTemplate 1.0.3-alpha

---

## Summary Table

| Date | AI Model | Build | Tests | L1 (/18) | L2 (/13) | L3 (/13) | L4 (/9) | L5 (/4) | Total (/57) | Verdict |
|------|----------|-------|-------|----------|----------|----------|---------|---------|-------------|---------|
| 2025-07-10 | Claude Opus 4.6 (Copilot) | 0 errors | 74/75 | 18/18 | 13/13 | 13/13 | 5/9 | 4/4 | **53/57** | **PASS** |

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
