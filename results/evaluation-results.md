# Trellis AI Evaluation Results

Tracks how well different AI models implement the Order Management spec using Trellis conventions.

**Evaluation spec:** Order Management (see [`specs/order-management.md`](../specs/order-management.md))
**Scoring framework:** 57 criteria across 5 levels (see [`docs/training-lab.md`](../docs/training-lab.md))
**Goal:** Total score of 52+/57
**Trellis version:** 3.0.0-alpha.124
**Template version:** Trellis.AspTemplate 1.0.3-alpha

---

## Summary Table

| Date | AI Model | Build | Tests | L1 (/18) | L2 (/13) | L3 (/13) | L4 (/9) | L5 (/4) | Total (/57) | Verdict |
|------|----------|-------|-------|----------|----------|----------|---------|---------|-------------|---------|
| 2026-03-22 | GPT-5.4 (Copilot) | 0 errors | 34/34 | 17/18 | 13/13 | 13/13 | 9/9 | 3/4 | **55/57** | **PASS** |
| 2026-03-22 | Claude Sonnet 4.6 (Copilot) | 0 errors | 57/57 | 18/18 | 13/13 | 13/13 | 9/9 | 4/4 | **57/57** | **PASS** |
| 2026-03-22 | Claude Opus 4.6 (Copilot) | 0 errors | 41/41 | 17/18 | 13/13 | 13/13 | 9/9 | 4/4 | **56/57** | **PASS** |

---

## Detailed Scorecard: GPT-5.4 (Copilot)

**Date:** 2026-03-22
**Model:** GPT-5.4 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 34/34 (all pass)
**Duration:** ~28 minutes (fastest of the three)

### Level 1: Structural Consistency — 17/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All required value objects present |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\> |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | `StateMachine<string, string>` with lazy init pattern |
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
| 17 | build/test.props shared (T) | **PASS** | `build/test.props` exists |
| 18 | No primitive obsession | **FAIL** | GlobalUsings.cs exists in test projects (Api.Tests, Application.Tests, Acl.Tests). Convention says only build/test.props should handle global usings. |

### Level 2: Behavioral Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler validates stock reservation per line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | Releases stock from Submitted/Approved, skips for Draft |
| 3 | Line item price snapshot | **PASS** | UnitPrice (Money) captured at creation |
| 4 | Duplicate product in order | **PASS** | AddLineItem rejects duplicate product |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError used per spec |
| 7 | Order total computed | **PASS** | Money.Multiply + Money.Add used correctly |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use `RequiredGuid<T>` with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler |
| 12 | Cancel resource auth check | **PASS** | CancelOrderCommand: IAuthorizeResource\<Order\> with ownership check |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories |

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` with both assemblies |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + versioned controller namespace |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` |
| 11 | DTOs in Api layer | **PASS** | Models in versioned Api Models directory |
| 12 | EF Core entity configurations | **PASS** | IEntityTypeConfiguration classes in Acl |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 9/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | Domain tests covering aggregates, value objects, state machine |
| 2 | Happy path tests | **PASS** | All operations have at least one success test |
| 3 | Error path tests | **PASS** | Invalid transitions, validation failures, insufficient stock, duplicate product, last item tested |
| 4 | State machine tests | **PASS** | Valid + invalid transition tests |
| 5 | Specification test | **PASS** | Unit tests for OverdueOrderSpecification |
| 6 | Authorization tests | **PASS** | Permission and resource-auth tests present |
| 7 | Maybe assertion tests | **PASS** | Correct Trellis.Testing assertions used |
| 8 | API integration tests | **PASS** | Integration tests covering lifecycle, authorization, error cases |
| 9 | Trellis.Testing used | **PASS** | `.Should().BeSuccess()` / `.Should().BeFailure()` used correctly |

### Level 5: Feedback Quality — 3/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | Friction points with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | Specific Trellis features listed |
| 4 | Copilot instructions feedback | **FAIL** | TRELLIS_FEEDBACK.md lacks a dedicated "Copilot Instructions Feedback" section |

### Overall Assessment

**Score: 55/57 (96%) — PASS**

**Strengths:** Perfect L2-L4 (35/35). Fastest run at ~28 minutes. Clean architecture, correct patterns, `Maybe<T>.None`, `Result.ParallelAsync().WhenAllAsync()`, `HasTrellisIndex`, `AddResourceAuthorization` with both assemblies, `IAuthorizeResource<Order>` on CancelOrderCommand all used correctly. Massive improvement from previous 45/57 on alpha.106.

**Notable:** `IReadOnlyList<T>` vs `ReadOnlyCollection<T>` discrepancy discovered during this run. `MaybeQueryInterceptor` singleton registration issue also documented.

---

## Detailed Scorecard: Claude Sonnet 4.6 (Copilot)

**Date:** 2026-03-22
**Model:** Claude Sonnet 4.6 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 57/57 (all pass)
**Duration:** ~10 hours (build error retry loops)

### Level 1: Structural Consistency — 18/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All required value objects present including composite ShippingAddress |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\> |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | Stateless state machine with lazy init pattern |
| 6 | State transitions return Result | **PASS** | `Machine.FireResult(trigger)` returns Result |
| 7 | Domain events defined | **PASS** | All 5 events defined |
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
| 18 | No primitive obsession | **PASS** | No raw Guid/string/int in domain methods |

### Level 2: Behavioral Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler validates stock reservation per line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | Releases stock from Submitted/Approved, skips for Draft |
| 3 | Line item price snapshot | **PASS** | LineItem stores UnitPrice (Money) captured at creation |
| 4 | Duplicate product in order | **PASS** | AddLineItem rejects duplicate product |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError used per spec |
| 7 | Order total computed | **PASS** | Money.Multiply + Money.Add used correctly |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use `RequiredGuid<T>` with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler |
| 12 | Cancel resource auth check | **PASS** | CancelOrderCommand: IAuthorizeResource\<Order\> with ownership check + admin fallback |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories |

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` with both assemblies |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + versioned controller namespace |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` |
| 11 | DTOs in Api layer | **PASS** | Models in versioned Api Models directory |
| 12 | EF Core entity configurations | **PASS** | IEntityTypeConfiguration classes in Acl |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 9/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | Comprehensive domain tests covering all aggregates, value objects, state machine |
| 2 | Happy path tests | **PASS** | All operations have at least one success test |
| 3 | Error path tests | **PASS** | Invalid transitions, validation failures, insufficient stock, duplicate product, last item tested |
| 4 | State machine tests | **PASS** | Valid + invalid transition tests |
| 5 | Specification test | **PASS** | Unit tests for OverdueOrderSpecification |
| 6 | Authorization tests | **PASS** | Permission and resource-auth tests present |
| 7 | Maybe assertion tests | **PASS** | Correct Trellis.Testing `.Should().HaveValue()` / `.Should().BeNone()` used |
| 8 | API integration tests | **PASS** | Integration tests covering lifecycle, authorization, error cases |
| 9 | Trellis.Testing used | **PASS** | `.Should().BeSuccess()` / `.Should().BeFailure()` used correctly |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | Friction points with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | Specific Trellis features listed |
| 4 | Copilot instructions feedback | **PASS** | Dedicated section with instruction clarity feedback |

### Overall Assessment

**Score: 57/57 (100%) — PASS**

**Strengths:** First model to achieve a perfect 57/57. Perfect across all 5 levels. 57/57 tests pass with zero failures. All Trellis conventions used correctly: `Maybe<T>.None`, `Result.ParallelAsync().WhenAllAsync()`, `HasTrellisIndex`, `AddResourceAuthorization` with both assemblies, `IAuthorizeResource<Order>` on CancelOrderCommand. Trellis.Testing assertions used correctly throughout.

**Notable:** Took ~10 hours due to build error retry loops — significantly longer than the other two models. Despite the time cost, the output quality was flawless.

---

## Detailed Scorecard: Claude Opus 4.6 (Copilot)

**Date:** 2026-03-22
**Model:** Claude Opus 4.6 (via GitHub Copilot agent mode)
**Build result:** 0 errors, 0 warnings
**Test result:** 41/41 (all pass)
**Duration:** ~58 minutes

### Level 1: Structural Consistency — 17/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All required value objects present |
| 2 | Value objects use TryCreate | **PASS** | All use static TryCreate returning Result\<T\> |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order extend Aggregate\<TId\> |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **PASS** | Stateless state machine with lazy init pattern |
| 6 | State transitions return Result | **PASS** | `Machine.FireResult(trigger)` returns Result |
| 7 | Domain events defined | **PASS** | All 5 events defined |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with parameterized DateTime |
| 9 | CQRS pattern used | **PASS** | Commands/queries with handlers via Mediator |
| 10 | Authorization on commands | **PASS** | All commands implement IAuthorize; CancelOrderCommand implements IAuthorizeResource\<Order\> |
| 11 | Permissions as constants | **PASS** | Constants in Domain Permissions class |
| 12 | Repository interfaces in Application | **PASS** | ICustomerRepository, IProductRepository, IOrderRepository in Application |
| 13 | EF Core in Acl (T) | **PASS** | DbContext and repository implementations in Acl |
| 14 | ApplyTrellisConventions used (T) | **PASS** | In ConfigureConventions; zero HasConversion() calls |
| 15 | Project structure matches template (T) | **PASS** | Correct 4-project structure with correct dependency direction |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain and Application layers |
| 17 | build/test.props shared (T) | **FAIL** | build/test.props file not found by scorer |
| 18 | No primitive obsession | **PASS** | No raw Guid/string/int in domain methods |

### Level 2: Behavioral Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler validates stock reservation per line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | Releases stock from Submitted/Approved, skips for Draft |
| 3 | Line item price snapshot | **PASS** | LineItem stores UnitPrice (Money) captured at creation |
| 4 | Duplicate product in order | **PASS** | AddLineItem rejects duplicate product |
| 5 | Last line item protection | **PASS** | RemoveLineItem fails when 1 item remains |
| 6 | Error types match | **PASS** | ValidationError, NotFoundError, ConflictError used per spec |
| 7 | Order total computed | **PASS** | Money.Multiply + Money.Add used correctly |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use `RequiredGuid<T>` with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler |
| 12 | Cancel resource auth check | **PASS** | CancelOrderCommand: IAuthorizeResource\<Order\> with ownership check + admin fallback |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories |

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` with both assemblies |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + versioned controller namespace |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` |
| 11 | DTOs in Api layer | **PASS** | Models in versioned Api Models directory |
| 12 | EF Core entity configurations | **PASS** | IEntityTypeConfiguration classes in Acl |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

### Level 4: Test Consistency — 9/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | Comprehensive domain tests |
| 2 | Happy path tests | **PASS** | All operations have at least one success test |
| 3 | Error path tests | **PASS** | Invalid transitions, validation failures, insufficient stock, duplicate product, last item tested |
| 4 | State machine tests | **PASS** | Valid + invalid transition tests |
| 5 | Specification test | **PASS** | Unit tests for OverdueOrderSpecification |
| 6 | Authorization tests | **PASS** | Comprehensive authorization tests including resource-level auth |
| 7 | Maybe assertion tests | **PASS** | Correct Trellis.Testing assertions used |
| 8 | API integration tests | **PASS** | Integration tests covering lifecycle, authorization, error cases |
| 9 | Trellis.Testing used | **PASS** | `.Should().BeSuccess()` / `.Should().BeFailure()` used correctly |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | Friction points with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | Specific Trellis features listed |
| 4 | Copilot instructions feedback | **PASS** | Dedicated section with instruction clarity feedback |

### Overall Assessment

**Score: 56/57 (98%) — PASS**

**Strengths:** Near-perfect score. 41 tests in ~58 minutes with comprehensive coverage including authorization tests. Perfect L2-L5 (39/39). All key Trellis patterns used correctly: `Maybe<T>.None`, `Result.ParallelAsync().WhenAllAsync()`, `HasTrellisIndex`, `AddResourceAuthorization` with both assemblies, `IAuthorizeResource<Order>` on CancelOrderCommand.

**Notable:** Only failure was build/test.props not found by scorer — a structural issue rather than a conceptual one. Produced comprehensive authorization tests that the previous Opus 4.6 run (alpha.104) lacked entirely.

---

## Key Observations

### Cross-Model Consistency

All 3 models achieved **perfect L2 (Behavioral), L3 (Architecture), and L4 (Tests)** — demonstrating that the framework + copilot instructions produce consistent business logic, architecture, and test coverage regardless of model. This is a significant milestone.

### Patterns Correctly Used by All 3 Models

- `Maybe<T>.None` for optional properties
- `Result.ParallelAsync().WhenAllAsync()` for parallel validation
- `HasTrellisIndex` for composite indexes
- `AddResourceAuthorization` with both assemblies
- `IAuthorizeResource<Order>` on CancelOrderCommand
- `SaveChangesResultUnitAsync` in all repositories
- Zero try/catch — pure ROP throughout

### Performance vs. Quality

| Model | Duration | Score | Tests |
|-------|----------|-------|-------|
| GPT-5.4 | ~28 min | 55/57 | 34 |
| Opus 4.6 | ~58 min | 56/57 | 41 |
| Sonnet 4.6 | ~10 hrs | 57/57 | 57 |

Sonnet 4.6 is the first model to achieve 57/57 but took ~10 hours due to build error retry loops. GPT-5.4 was fastest at ~28 minutes. Opus 4.6 hit a good balance at ~58 minutes with 41 tests and comprehensive authorization coverage.

### Issues Discovered

- **`IReadOnlyList<T>` vs `ReadOnlyCollection<T>`** — inconsistency discovered and documented via this run
- **`MaybeQueryInterceptor` singleton** — registration issue identified and documented

---

---

## Level 6: Feature Addition — Order Returns (Step 8)

Each model extended its own Part 1 implementation with the Order Returns feature. This tests incremental modification of an existing Trellis codebase.

### Summary

| Model | Part 1 Tests | Part 2 Tests | New Tests | Regressions | Runtime | Verdict |
|-------|:------------:|:------------:|:---------:|:-----------:|--------:|---------|
| GPT-5.4 | 34 | **42** | +8 | 0 | 7.5 min | **PASS** |
| Claude Opus 4.6 | 41 | **49** | +8 | 0 | 13 min | **PASS** |
| Claude Sonnet 4.6 | 57 | **70** | +13 | 0 | 23 min | **PASS** |

### What Each Model Added

All three models correctly implemented:
- ✅ `Returned` added to OrderStatus enum + state machine transition `Delivered → Returned`
- ✅ `ReturnReason` value object (10–500 char validation via TryCreate)
- ✅ `DeliveredAt` and `ReturnedAt` as `partial Maybe<DateTime>` on Order
- ✅ 30-day return window validation (testable with injectable date)
- ✅ Stock released on return (same pattern as cancel)
- ✅ `ReturnOrderCommand` with `orders:return` permission via IAuthorize
- ✅ `POST /api/orders/{id}/return` endpoint with correct status codes
- ✅ `OrderReturnedEvent` domain event
- ✅ Domain + Application + API tests for return scenarios
- ✅ Zero regressions — all pre-existing tests still pass

### Key Observations

- **All three achieved zero regressions** — the Trellis 4-layer architecture and state machine pattern enabled safe incremental modification.
- **GPT-5.4 was fastest** (7.5 min) — confident, surgical changes with minimal retry.
- **Sonnet added the most tests** (+13 vs +8) including edge cases and also fixed bugs in its own repository implementations discovered during the feature addition.
- **Stock release reuse** — all three recognized that return stock release is the same pattern as cancel and reused the existing logic.
- **State machine extension** — all three correctly added the new transition without breaking existing transitions.

---

## Historical Comparison

Previous scores on alpha.104/alpha.106 vs. current alpha.124:

| Model | Previous Version | Previous Score | Current Score (alpha.124) | Delta |
|-------|-----------------|---------------|--------------------------|-------|
| Claude Opus 4.6 | alpha.104 | 53/57 | 56/57 | **+3** |
| GPT-5.4 | alpha.106 | 45/57 | 55/57 | **+10** |
| Claude Sonnet 4.6 | alpha.106 | 55/57 | 57/57 | **+2** |

The improvement from prior baselines to alpha.124 demonstrates the value of the improvement loop: scoring runs surface friction points → friction points drive copilot instruction and framework refinements → subsequent runs score higher. GPT-5.4's +10 jump (from FAIL to PASS) is the most dramatic improvement, largely driven by L4 test coverage going from 1/9 to 9/9.
