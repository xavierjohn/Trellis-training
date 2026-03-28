# Evaluation Criteria — Order Management Lab

## Evaluation Criteria

> **Template baseline:** The `dotnet new trellis-asp` template pre-provides project structure, build system files, and test infrastructure. Level 1 structural criteria marked with **(T)** are satisfied by the template. They measure whether the AI *preserved* the template structure — a failure means the AI broke or replaced something that was already correct.

### Level 1: Structural Consistency (Pass/Fail)

These must be identical across all 10 runs. If any vary, it indicates a missing building block in Trellis.

| Criterion | What to Compare | Target |
|-----------|----------------|--------|
| **Value objects exist** | CustomerId, OrderId, ProductId, LineItemId, Sku, Money, ShippingAddress, FirstName, LastName, ProductName, Quantity are present as distinct types | 10/10 identical |
| **Value objects use TryCreate** | All value objects use static TryCreate returning Result\<T\> | 10/10 identical |
| **Aggregates inherit correctly** | Customer, Product, Order extend Aggregate\<TId\> | 10/10 identical |
| **Line items are entities** | LineItem extends Entity\<LineItemId\> | 10/10 identical |
| **State machine uses Stateless** | Order status transitions configured via Stateless with FireResult | 10/10 identical |
| **State transitions return Result** | Fire operations return Result\<Order\>, not void/throw | 10/10 identical |
| **Domain events defined** | All 5 events from spec exist as classes/records | 10/10 identical |
| **Specification exists** | OverdueOrderSpec implements Specification\<Order\> | 10/10 identical |
| **CQRS pattern used** | All operations are Commands/Queries with Handlers using Mediator | 10/10 identical |
| **Authorization on commands** | Commands implement IAuthorize with RequiredPermissions. CancelOrderCommand implements IAuthorizeResource | 10/10 identical |
| **Permissions as constants** | Permission strings defined as constants in Domain layer Permissions class | 10/10 identical |
| **Repository interfaces in Application** | ICustomerRepository, IProductRepository, IOrderRepository in Application, not Domain | 10/10 identical |
| **EF Core in Acl** **(T)** | DbContext and repository implementations in Acl, not Application or Domain | 10/10 identical |
| **ApplyTrellisConventions used** **(T)** | ConfigureConventions calls ApplyTrellisConventions — no manual HasConversion() | 10/10 identical |
| **Project structure matches template** **(T)** | Folder structure follows the project layout in copilot instructions | 10/10 identical |
| **No exceptions for control flow** | grep for try/catch in Domain and Application layers returns zero hits | 10/10 identical |
| **build/test.props shared** **(T)** | `build/test.props` exists. No `GlobalUsings.cs` in test projects. | 10/10 identical |
| **No primitive obsession** | grep for raw Guid, raw string parameters in domain methods returns zero hits | 10/10 identical |
| **Handlers return domain types** | No DTO types in Application layer — handlers return `Result<Order>`, `Result<Customer>`, etc. | 10/10 identical |
| **Repository returns Maybe** | Repository `FindByIdAsync` returns `Maybe<T>`, not `Result<T>` — handler converts with `.ToResult(Error.NotFound(...))` | 10/10 identical |

### Level 2: Behavioral Consistency (Scored)

These should be highly consistent. Minor naming variations acceptable; logic must be equivalent.

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Submit validates stock** | Submit transition checks stock availability before reserving | 10 = all correct, <7 = needs pattern |
| **Cancel releases stock** | Cancel from Submitted/Approved restores stock, Cancel from Draft does not | 10 = all correct, <7 = needs pattern |
| **Line item price snapshot** | UnitPrice captured from product at creation, not referenced live | 10 = all correct, <7 = needs pattern |
| **Duplicate product in order** | Adding same product to order is rejected | 10 = all handle it, <7 = spec ambiguity |
| **Last line item protection** | Cannot remove last line item from order | 10 = all enforce, <7 = needs pattern |
| **Error types match** | ValidationError, NotFoundError, ConflictError, ForbiddenError used correctly per spec | 10 = all match, <7 = error taxonomy issue |
| **Order total computed** | Order total calculated as sum of (quantity × unitPrice) | 10 = all compute, <7 = needs guidance |
| **Overdue spec correct** | Spec checks Submitted status + 7-day threshold, translatable to SQL | 10 = all correct, <7 = spec clarity |
| **IDs use RequiredGuid with V7** | All identity types use RequiredGuid with Guid.CreateVersion7() | 10 = all correct, <7 = needs guidance |
| **Maybe for optional phone** | Customer.PhoneNumber is Maybe\<PhoneNumber\>, stored as nullable column | 10 = all correct, <7 = needs pattern |
| **ParallelAsync for draft order** | CreateDraftOrder fetches customer and products in parallel | 10 = all use ParallelAsync, <7 = needs example |
| **Cancel resource auth check** | CancelOrderCommand checks actor == owner OR admin | 10 = all correct, <7 = needs pattern |
| **SaveChangesResultAsync used** | Repositories use SaveChangesResultAsync, not bare SaveChangesAsync | 10 = all correct, <7 = needs guidance |
| **Result.Ensure for auth** | Resource authorization uses `Result.Ensure(condition, Error.Forbidden(...))` instead of ternary | 10 = all correct, <7 = needs pattern |
| **Natural VO in specs** | Specifications use `order.SubmittedAt < cutoff` without `.Value` | 10 = all correct, <7 = needs interceptor guidance |

### Level 3: Architecture & API Consistency (Scored)

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Clean architecture layers** | Four projects with correct dependency direction | 10 = all match, <7 = needs guidance |
| **Domain has no external deps** | Domain .csproj references only Trellis packages and .NET runtime | 10 = all clean, <7 = dependency violation |
| **Pipeline behaviors registered** | Mediator registered with pipeline behaviors from Trellis.Mediator | 10 = all correct, <7 = needs guidance |
| **IActorProvider registered** | TestActorProvider registered, reads X-Test-Actor header | 10 = all correct, <7 = needs pattern |
| **DI extension per layer** | Each layer has one DI extension method, wired in Program.cs | 10 = all match, <7 = template unclear |
| **Endpoint paths match** | All 16 endpoints exist with correct HTTP methods and paths | 10 = exact match, <7 = spec needs detail |
| **API versioning configured** | Asp.Versioning with namespace convention, versioned controller folders | 10 = all present, <7 = needs emphasis |
| **Problem Details for errors** | Error responses follow RFC 9457 format | 10 = all use it, <7 = Trellis.Asp gap |
| **201 for creation with Location** | POST /customers and POST /orders return 201 with Location header | 10 = all correct, <7 = needs pattern |
| **Health check endpoint** | /health endpoint present | 10 = all present, <7 = needs emphasis |
| **DTOs in Api layer** | Request/Response types in versioned Models/ folder (e.g., `Api/src/{version}/Models/`), not domain types | 10 = all correct, <7 = needs example |
| **EF Core entity configurations** | IEntityTypeConfiguration classes in Acl | 10 = all correct, <7 = needs guidance |
| **EnsureCreated on startup** | Database created via `EnsureCreated()` in development mode, no EF Core migrations | 10 = all correct, <7 = needs instruction |
| **api.http updated** | Template api.http replaced with requests covering all 16 endpoints, correct api-version, X-Test-Actor headers, happy path + error examples | 10 = all endpoints, <7 = still has scaffold defaults |
| **api.http playback passes** | All api.http requests execute successfully against the running service: happy-path requests return expected status codes (201, 200), error-path requests return expected error codes (400, 409, 403, 404). No requests fail due to invalid test data (e.g., SKU format mismatches, wrong field names). | 10 = all pass, <7 = some requests fail |
| **AddTrellisInterceptors** | `AddTrellisInterceptors()` called in DbContext options for natural VO LINQ support | 10 = all correct, <7 = needs guidance |

### Level 4: Test Consistency (Scored)

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Domain tests exist** | Unit tests for each aggregate's business rules | 10 = all present, <7 = needs guidance |
| **Happy path tests** | Each operation has at least one success test | 10 = all present |
| **Error path tests** | Invalid transitions, validation failures, not found tested | 10 = all present, <7 = needs patterns |
| **State machine tests** | Each valid and invalid transition tested | 10 = all present, <7 = needs helpers |
| **Specification test** | OverdueOrderSpec tested | 10 = all present |
| **Authorization tests** | Permission denied and resource auth tests present | 10 = all present, <7 = needs patterns |
| **Maybe assertion tests** | Tests use `.Should().HaveValue()` / `.Should().BeNone()` for optional phone | 10 = all present, <7 = needs guidance |
| **API integration tests** | Tests verify HTTP round-trips, routing, versioning, status codes, and 403 on missing permission | 10 = all present, <7 = needs guidance |
| **Trellis.Testing used** | Tests use `.Should().BeSuccess()` / `.Should().BeFailure()` instead of manual inspection | 10 = all present, <7 = needs emphasis |

### Level 5: Feedback Quality (Scored)

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Feedback file exists** | TRELLIS_FEEDBACK.md generated in repository root | 10 = all present, <7 = instructions unclear |
| **Friction points are specific** | Each FP has category, severity, context, workaround, and suggestion | 10 = all actionable, <7 = needs format guidance |
| **What Worked Well present** | Positive feedback section exists with specific Trellis features listed | 10 = all present, <7 = needs emphasis |
| **Copilot instructions feedback** | AI identifies ambiguities or gaps in the copilot instructions | 10 = all present, <7 = needs prompting |

### Level 6: Feature Addition (Scored)

> These criteria evaluate Step 8 — the Order Returns feature added to an existing codebase. They measure whether the AI can modify Trellis patterns without regressions.

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Zero regressions** | All pre-existing tests still pass after feature addition | 10 = all pass, <7 = AI broke existing code |
| **Returned status added** | `Returned` added to OrderStatus enum; state machine updated with `Delivered → Returned` transition | 10 = all correct, <7 = state machine gap |
| **ReturnReason value object** | `ReturnReason` uses TryCreate with 10-500 char validation | 10 = all correct, <7 = VO pattern gap |
| **DeliveredAt tracked** | `DeliveredAt` is `Maybe<DateTime>` on Order, set during Delivered transition | 10 = all correct, <7 = Maybe pattern gap |
| **30-day return window** | Return validates `DeliveredAt` within 30 days; uses `TimeProvider` (optional parameter defaulting to `TimeProvider.System`) for testable time validation | 10 = all correct, <7 = needs time pattern |
| **Stock released on return** | Return releases reserved stock for each line item (same pattern as cancel) | 10 = all correct, <7 = pattern reuse gap |
| **ReturnOrderCommand pipeline** | Command implements IAuthorize, permission `orders:return`, handler wired through Mediator | 10 = all correct, <7 = CQRS modification gap |
| **API endpoint correct** | `POST /api/orders/{id}/return` with versioning, correct status codes | 10 = all correct, <7 = endpoint pattern gap |
| **Domain event raised** | `OrderReturnedEvent` with OrderId, CustomerId, ReturnReason, ReturnedAt | 10 = all present, <7 = event pattern gap |
| **Return tests exist** | Domain + API tests for valid return, expired window, invalid status | 10 = all present, <7 = test modification gap |

## How to Score

### Step 1: Build the Scorecard

Create a table with one row per criterion and one column per run.

```
                              Run  1  2  3  4  5  6  7  8  9  10  Consistency
L1: Value objects exist        [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ]  __/10
L1: Value objects use TryCreate[ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ] [ ]  __/10
...
```

### Step 2: Score Each Cell

For each criterion in each run, mark **Pass (✓)** or **Fail (✗)**.

**How to check:**
- **Structural criteria (L1):** Open the generated code and verify the type/pattern exists exactly as specified. Binary — it's there or it isn't.
- **Behavioral criteria (L2):** Read the implementation logic. Does it do what the spec says? Minor naming differences are fine. The logic must be equivalent.
- **Architecture & API criteria (L3):** Check project references, folder locations, endpoint routes, and configuration. Use `dotnet build` and manual inspection.
- **Test criteria (L4):** Check that the test file exists and covers the described scenario. Run `dotnet test` to verify tests pass.

**Quick checks you can script:**

```bash
# L1: No exceptions in domain/application
grep -r "try\s*{" Domain/src/ Application/src/ | wc -l
# Should be 0

# L1: No raw Guid in domain methods
grep -rn "Guid " Domain/src/ --include="*.cs" | grep -v "RequiredGuid" | wc -l
# Should be 0

# L1: No HasConversion in Acl
grep -r "HasConversion" Acl/src/ --include="*.cs" | wc -l
# Should be 0

# L1: ApplyTrellisConventions present
grep -r "ApplyTrellisConventions" Acl/src/ --include="*.cs" | wc -l
# Should be 1

# L1: CQRS commands/queries
grep -r "ICommand<Result" Application/src/ --include="*.cs" | wc -l
# Should be 11 (one per command)

# L1: IAuthorize on commands
grep -r "IAuthorize" Application/src/ --include="*.cs" | wc -l
# Should be >= 11

# L1: build/test.props exists
test -f build/test.props && echo "EXISTS" || echo "MISSING"

# L1: No GlobalUsings.cs in test projects
find . -path "*/tests/GlobalUsings.cs" | wc -l
# Should be 0

# L3: Controller action count
grep -rE "\[Http(Get|Post|Delete)\]" Api/src/ --include="*.cs" | wc -l
# Should be 16

# L3: EnsureCreated used (no migrations)
grep -r "EnsureCreated" Api/src/ --include="*.cs" | wc -l
# Should be >= 1

# L4: Test count
dotnet test --list-tests 2>/dev/null | grep "Test" | wc -l

# L5: Feedback file exists
test -f TRELLIS_FEEDBACK.md && echo "EXISTS" || echo "MISSING"

# L6: Returned status exists
grep -r "Returned" Domain/src/ --include="*.cs" | wc -l
# Should be >= 1

# L6: ReturnReason value object
grep -rn "ReturnReason" Domain/src/ --include="*.cs" | wc -l
# Should be >= 1

# L6: Return endpoint exists
grep -r "return" Api/src/ --include="*.cs" | grep -i "HttpPost\|return" | wc -l
# Should be >= 1

# L6: OrderReturnedEvent exists
grep -r "OrderReturnedEvent" Domain/src/ --include="*.cs" | wc -l
# Should be >= 1

# L6: Zero regressions
dotnet test 2>/dev/null | tail -5
# Should show all tests passed
```

### Step 3: Calculate Consistency Per Criterion

```
Consistency = number of runs where this criterion passed (out of 10)
```

### Step 4: Calculate Level Scores

A criterion **counts toward the level score** if its consistency is **7 or higher** (at least 7/10 runs got it right).

```
Level score = number of criteria in that level with consistency ≥ 7
```

| Level | Criteria Count | Score Range |
|-------|---------------|-------------|
| L1: Structural | 20 | 0–20 |
| L2: Behavioral | 15 | 0–15 |
| L3: Architecture & API | 16 | 0–16 |
| L4: Tests | 9 | 0–9 |
| L5: Feedback | 4 | 0–4 |
| L6: Feature Addition | 10 | 0–10 |
| **Total** | **74** | **0–74** |

### Step 5: Record in Tracking Table

| Date | Trellis Version | AI Model | L1 (/20) | L2 (/15) | L3 (/16) | L4 (/9) | L5 (/4) | L6 (/10) | Total (/74) | Notes |
|------|----------------|----------|----------|---------|----------|---------|---------|----------|-------------|-------|
| 2026-03-28 | 3.0.0-alpha.137 | Claude Opus 4.6 | — | — | — | — | 4/4 | — | — | 81 tests, 27 min, 4 FPs |
| 2026-03-28 | 3.0.0-alpha.137 | GPT-5.4 | — | — | — | — | 4/4 | — | — | 81 tests, 42 min, 4 FPs |
| 2026-03-28 | 3.0.0-alpha.137 | Claude Sonnet 4.6 | — | — | — | — | 4/4 | — | — | 78 tests, 83 min, 5 pain points |

> **Note:** L1–L4, L6 require 10 runs to score consistency. These 3 runs are a single-pass validation.
> All 3 models: build 0 errors/0 warnings, all tests pass, feedback file generated.

### Recurring Friction Points (across models)

| Issue | Models Hit | Priority |
|-------|-----------|----------|
| Money in request DTOs — need MoneyDto pattern | GPT-5.4, Sonnet | Document MoneyDto pattern in copilot instructions |
| TRLS001 `_ = result` workaround not obvious | Opus, Sonnet | Document in copilot instructions for test code |
| `[CustomerResourceId]` attribute undiscoverable | Sonnet | Document or replace with `[FromRoute]` |
| RequiredEnum handled by ApplyTrellisConventions — no HasConversion needed | GPT-5.4 | Clarify in copilot instructions |

### Step 6: Identify What to Fix

Sort all criteria by consistency score, lowest first:

| Consistency | Meaning | Action |
|-------------|---------|--------|
| **10/10** | Trellis fully constrains this. No work needed. | None |
| **9/10** | Almost there. One outlier run. | Check if the outlier is a fluke or a real gap. |
| **7–8/10** | Guidance exists but not enough constraint. | Add a stronger pattern, better example, or analyzer rule. |
| **4–6/10** | Significant gap. AI has too many choices. | Add a new building block, convention, or constraint. |
| **0–3/10** | Trellis doesn't address this at all. | Major addition needed. |

### The Goal

**Total score of 68+ out of 74** — meaning at least 68 of the 74 criteria achieve 7+/10 consistency across independent AI runs.

The Level 6 criteria specifically measure whether the architecture and copilot instructions support **incremental change** — the most important real-world capability. A model that scores 61/61 on L1–L5 but can't modify the codebase without regressions isn't production-ready.

---

## Trellis Packages Exercised

| Package | How It's Exercised |
|---------|--------------------|
| `Trellis.Results` | Result\<T\> on every operation, Maybe\<T\> for optionals, error types, Combine, Bind, Map, Tap, Match, MatchError, ParallelAsync |
| `Trellis.Primitives` | RequiredString, RequiredGuid (V7), RequiredInt, RequiredDecimal, RequiredEnum, EmailAddress, PhoneNumber |
| `Trellis.Primitives.Generator` | Source-generated TryCreate, equality, JSON converters for value objects |
| `Trellis.DomainDrivenDesign` | Aggregate\<T\>, Entity\<T\>, Specification\<T\>, domain events |
| `Trellis.Stateless` | Order state machine with Result-returning FireResult |
| `Trellis.Analyzers` | Compile-time ROP correctness checks |
| `Trellis.Asp` | ToActionResult(this), ToCreatedAtActionResult for 201+Location, Problem Details, scalar value binding |
| `Trellis.Authorization` | Actor, IActorProvider, IAuthorize (permissions), IAuthorizeResource (cancel ownership) |
| `Trellis.Mediator` | Commands, Queries, ValidationBehavior, AuthorizationBehavior, ResourceAuthorizationBehavior |
| `Trellis.EntityFrameworkCore` | ApplyTrellisConventions, AddTrellisInterceptors, SaveChangesResultAsync, FirstOrDefaultMaybeAsync, .Where(spec), ScalarValueQueryInterceptor for natural VO LINQ |
| `Trellis.Testing` | `.Should().BeSuccess()`, `.Should().BeFailure()`, `.Should().BeFailureOfType<T>()`, `.Should().HaveValue()`, `.Should().BeNone()`, `FakeRepository`, `ResultBuilder`, `ValidationErrorBuilder` |
