# Trellis Training Lab — Building Services with AI

> **Purpose:** Measure how consistently AI builds enterprise services using the Trellis framework. Give the AI the spec + copilot instructions and let it implement the entire service in one shot. This same guide doubles as an eval — run it 10 times to measure consistency.

## Prerequisites

- GitHub Copilot access (Copilot Chat in VS Code)
- .NET 10 SDK installed
- VS Code or Visual Studio
- Docker Desktop (optional — for Aspire Dashboard telemetry viewer)
- Trellis ASP template installed (`dotnet new install Trellis.AspTemplate`)
- Basic understanding of C# and web APIs

---

## Step 1: Create a Project Directory

```bash
mkdir OrderManagement
cd OrderManagement
git init
```

---

## Step 2: Start the Aspire Dashboard

The Aspire Dashboard lets you view traces, metrics, and structured logs. Run it locally via Docker:

```powershell
docker run --rm -it -d -p 18888:18888 -p 4317:18889 -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

| Port | Purpose |
|------|---------|
| `18888` | Dashboard UI — open http://localhost:18888 |
| `4317` | OTLP gRPC receiver — apps send telemetry here |

Verify it's running:

```bash
docker ps --format "table {{.Image}}\t{{.Ports}}\t{{.Status}}"
```

---

## Step 3: Scaffold with Template and Add Spec

1. Install the Trellis template (first time only — skip if already installed):

```bash
dotnet new install Trellis.AspTemplate
```

2. Scaffold the project:

```bash
dotnet new trellis-asp -n OrderManagement --authorName "Your Name"
```

This creates the full solution structure including:
- `.github/copilot-instructions.md` — Trellis conventions for AI
- `.github/trellis-api-reference.md` — Complete Trellis API surface reference
- All project files, build system (`Directory.Build.props`, `Directory.Packages.props`, `build/test.props`), and test infrastructure
- `.gitignore` configured for .NET/Visual Studio
- Working sample code (BestWeatherForecast) replaced with your service name

3. Verify the template builds and tests pass:

```bash
dotnet build
dotnet test
```

All 38 template tests should pass before you proceed.

4. Commit:

```bash
git add -A
git commit -m "Scaffold with Trellis template"
```

> **Why this approach?** The `dotnet new` template handles all scaffolding — project structure, build system, package references, global usings, and DI wiring. This eliminates token waste on boilerplate and ensures the AI focuses exclusively on implementing business logic. The copilot instructions (`.github/copilot-instructions.md`) tell the AI *how* to build with Trellis, and the API reference (`.github/trellis-api-reference.md`) gives it the full type surface.

---

## Step 4: Implement the Service

Open Copilot Chat, paste the **entire contents** of `specs/order-management.md` as context, and follow it with this prompt:

> Implement the Order Management service according to the spec above. Replace the existing sample code with the Order Management domain.

**Alternate prompt (SQL Server):** If you prefer SQL Server over SQLite, add to the prompt: *"Use SQL Server instead of SQLite. Use a separate console app to apply EF Core migrations instead of applying them on web service startup."*

**Let the AI work.** Do not intervene unless it asks a clarifying question. If it asks, answer with: "Follow the spec and copilot instructions."

**When it finishes, verify the build:**

```bash
dotnet build
dotnet test
```

If there are build or test errors, paste them back to Copilot and let it fix them. Repeat until clean.

---

## Step 5: Manual Smoke Test

Start the application with telemetry pointed at the Aspire Dashboard:

**PowerShell:**
```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
dotnet run --project Api/src
```

**Bash:**
```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 OTEL_EXPORTER_OTLP_PROTOCOL=grpc dotnet run --project Api/src
```

Open the **Aspire Dashboard** at http://localhost:18888 to view traces, metrics, and structured logs as you test.

Use the `.http` file with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension in VS Code. The smoke test should cover:

1. **Create a customer** (as SalesRep) → expect `201 Created` with Location header
2. **Create a customer without phone** → expect `201 Created`, PhoneNumber absent/null
3. **Create a product** (as WarehouseManager) → expect `201 Created`
4. **Add stock** → expect `200`, stock updated
5. **Create draft order** (as SalesRep) → expect `201 Created`, status = Draft
6. **Submit the order** → expect `200`, status = Submitted
7. **Cancel as different SalesRep** → expect `403 Forbidden` (not the owner)
8. **Approve without permission** → expect `403 Forbidden`
9. **Approve as WarehouseManager** → expect `200`
10. **Cancel as original creator** → expect `200`, stock restored
11. **Health check** → expect `200`

If any response is wrong, note it for evaluation but **do not fix it** during eval runs.

---

## Step 6: Review and Commit

1. Review all generated code against the evaluation criteria below.
2. Note issues but **do not fix them** during eval runs — they become your scores.
3. Commit:

```bash
git add -A
git commit -m "Implement Order Management Service with Trellis"
```

---

## Step 7: Generate Trellis Feedback

Ask Copilot to reflect on the development experience:

> Review the entire codebase you just built. Generate a TRELLIS_FEEDBACK.md file following the format in the copilot instructions. Be specific about any friction points, workarounds, or missing features you encountered. Also note what worked well.

**What to verify:**

- [ ] `TRELLIS_FEEDBACK.md` exists in the repository root
- [ ] Each friction point has a category, severity, context, and suggested improvement
- [ ] Workaround code is included where applicable
- [ ] "What Worked Well" section is present and specific
- [ ] Copilot Instructions Feedback section addresses any ambiguities encountered
- [ ] Feedback is actionable — the Trellis team can read each entry and decide whether to act on it

```bash
git add TRELLIS_FEEDBACK.md
git commit -m "Add Trellis feedback"
```

---

## Step 8: Add a Feature — Order Returns

> **Purpose:** Demonstrate that the architecture you just built supports incremental change. Give the AI a new business requirement and let it modify the existing codebase. This tests whether Trellis patterns hold up when requirements evolve — the real-world scenario.

### The Business Requirement

A new business rule has been approved: **customers can return delivered orders within 30 days.**

Paste this into Copilot Chat as a follow-up prompt (in the same conversation that built the service):

> **New requirement: Order Returns**
>
> Customers can now return delivered orders. Add the following to the existing Order Management service:
>
> **Domain changes:**
> - Add `Returned` to the OrderStatus enum
> - Add a `ReturnReason` value object — required string, 10–500 characters
> - Add `DeliveredAt` as a `Maybe<DateTime>` property on Order (set during Delivered transition)
> - Add `ReturnedAt` as a `Maybe<DateTime>` property on Order (set during Return transition)
> - Add state transition: `Delivered → Returned`
>   - Precondition: Order must have been delivered within the last 30 days (`DeliveredAt` must exist and be no more than 30 days ago)
>   - Side effect: Release reserved stock for each line item (same as cancel)
>   - Side effect: Set `ReturnedAt` to current UTC time
>   - Domain event: `OrderReturnedEvent(OrderId, CustomerId, ReturnReason, ReturnedAt)`
> - Shipped and Cancelled orders cannot be returned
> - Already-returned orders cannot be returned again
>
> **Application changes:**
> - Add `ReturnOrderCommand` with `orders:return` permission
> - Add `ReturnOrderHandler` — fetches order + products, validates return window, fires transition, releases stock, saves
> - Add permission: `orders:return` to Permissions class
> - SalesRep role gets `orders:return` permission
>
> **API changes:**
> - Add endpoint: `POST /api/orders/{id}/return` with body `{ "reason": "..." }`
> - Returns 200 OK with updated order on success
> - Returns 400 if return window expired or invalid transition
> - Returns 404 if order not found
> - Returns 403 if missing permission
>
> **Test changes:**
> - Domain: return within window succeeds, return after 30 days fails, return from non-Delivered status fails, stock released on return
> - Application: handler happy path, missing permission
> - API: HTTP round-trip for successful return, 400 for expired window
>
> **EF changes:**
> - Add `DeliveredAt` and `ReturnedAt` as `partial Maybe<DateTime>` properties on Order — the source generator and `MaybeConvention` handle persistence automatically

### What This Tests

This exercise specifically validates that:

| What | Why It Matters |
|------|---------------|
| **State machine modification** | Can the AI add a new status + transition to an existing Stateless machine without breaking existing transitions? |
| **New value object** | Does the source generator pattern hold for additions? |
| **Aggregate modification** | Can the AI add properties and methods to an existing aggregate? |
| **Stock release reuse** | Does the AI recognize that return stock release is the same pattern as cancel? |
| **Time-based business rule** | Like the overdue spec, this has a time constraint — does the AI make it testable (injectable date)? |
| **Existing test preservation** | Do ALL existing tests still pass after the modification? |
| **Full pipeline** | Does the new command wire through authorization, validation, handler, repository, controller, DTO correctly? |

### Verification

```bash
dotnet build    # 0 errors
dotnet test     # All previous tests pass + new return tests pass
```

**Check specifically:**
- [ ] Existing tests still pass (zero regressions)
- [ ] `Returned` exists in OrderStatus enum
- [ ] `ReturnReason` value object with TryCreate validation (10-500 chars)
- [ ] `DeliveredAt` is `Maybe<DateTime>` on Order, set during Delivered transition
- [ ] `ReturnedAt` is `Maybe<DateTime>` on Order, set during Return transition
- [ ] State machine allows `Delivered → Returned` only
- [ ] Return checks 30-day window from `DeliveredAt`
- [ ] Stock release runs on return (same as cancel from Submitted/Approved)
- [ ] `OrderReturnedEvent` raised with reason
- [ ] `orders:return` permission added to Permissions class
- [ ] `ReturnOrderCommand` implements `IAuthorize`
- [ ] `POST /api/orders/{id}/return` endpoint exists with correct versioning
- [ ] Domain tests cover: valid return, expired window, invalid source status
- [ ] API test covers HTTP round-trip

### Commit

```bash
git add -A
git commit -m "Add Order Returns feature"
```

---

## Congratulations

You've built a complete enterprise service and evolved it with a new feature. Your codebase demonstrates:

- Clean architecture (Domain, Application, Acl, API)
- CQRS with Mediator and pipeline behaviors
- Railway-Oriented Programming (no exceptions, typed errors)
- Domain-Driven Design (aggregates, value objects, entities, specifications)
- Permission-based and resource-based authorization
- State machine with safe transitions — and safe modification
- EF Core with convention-based value converters (zero HasConversion boilerplate)
- Maybe\<T\> for optional values (no nulls in domain model)
- API versioning and Service Level Indicators
- Health checks
- OpenTelemetry with Aspire Dashboard (traces, metrics, structured logs)
- Comprehensive domain, application, and API integration tests
- **Incremental feature addition without regressions**

---

# Running This as an Eval

To use this guide as a consistency eval for Trellis:

1. **Start 10 fresh sessions** — new repo, new Copilot conversation each time.
2. **Follow Steps 1–7 identically** in each session.
3. **After each session,** score the output against the Evaluation Criteria below.
4. **Record scores** in the tracking table.
5. **Identify lowest-scoring criteria** — these are gaps in Trellis or the Copilot Instructions.
6. **Improve Trellis or the instructions** to address the gaps.
7. **Run again** — scores should improve.
8. **Aggregate TRELLIS_FEEDBACK.md** across all 10 runs — recurring friction points are the highest-priority gaps.

### Tips for Consistent Eval Runs

- Use the same prompt in Step 4 exactly. Don't rephrase.
- Don't fix Copilot's mistakes during the eval — note them and score them.
- If Copilot asks a clarifying question, answer with "Follow the spec and Copilot instructions."
- Time each run. Consistent runs should take roughly the same amount of time.
- Save the generated code from each run for comparison.

### What You're Measuring

You're not measuring whether the AI can write code. You're measuring whether **Trellis constrains the AI enough** that 10 different runs produce the same architecture, the same patterns, and the same error handling. Where they diverge, Trellis needs a tighter building block.

---

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

### Level 3: Architecture & API Consistency (Scored)

| Criterion | What to Compare | Scoring |
|-----------|----------------|---------|
| **Clean architecture layers** | Four projects with correct dependency direction | 10 = all match, <7 = needs guidance |
| **Domain has no external deps** | Domain .csproj references only Trellis packages and .NET runtime | 10 = all clean, <7 = dependency violation |
| **Pipeline behaviors registered** | Mediator registered with pipeline behaviors from Trellis.Mediator | 10 = all correct, <7 = needs guidance |
| **IActorProvider registered** | TestActorProvider registered, reads X-Test-Actor header | 10 = all correct, <7 = needs pattern |
| **DI extension per layer** | Each layer has one DI extension method, wired in Program.cs | 10 = all match, <7 = template unclear |
| **Endpoint paths match** | All 14 endpoints exist with correct HTTP methods and paths | 10 = exact match, <7 = spec needs detail |
| **API versioning configured** | Asp.Versioning with namespace convention, versioned controller folders | 10 = all present, <7 = needs emphasis |
| **Problem Details for errors** | Error responses follow RFC 9457 format | 10 = all use it, <7 = Trellis.Asp gap |
| **201 for creation with Location** | POST /customers and POST /orders return 201 with Location header | 10 = all correct, <7 = needs pattern |
| **Health check endpoint** | /health endpoint present | 10 = all present, <7 = needs emphasis |
| **DTOs in Api layer** | Request/Response types in versioned Models/ folder (e.g., `Api/src/{version}/Models/`), not domain types | 10 = all correct, <7 = needs example |
| **EF Core entity configurations** | IEntityTypeConfiguration classes in Acl | 10 = all correct, <7 = needs guidance |
| **EnsureCreated on startup** | Database created via `EnsureCreated()` in development mode, no EF Core migrations | 10 = all correct, <7 = needs instruction |
| **api.http updated** | Template api.http replaced with requests covering all 14 endpoints, correct api-version, X-Test-Actor headers, happy path + error examples | 10 = all endpoints, <7 = still has scaffold defaults |
| **api.http playback passes** | All api.http requests execute successfully against the running service: happy-path requests return expected status codes (201, 200), error-path requests return expected error codes (400, 409, 403, 404). No requests fail due to invalid test data (e.g., SKU format mismatches, wrong field names). | 10 = all pass, <7 = some requests fail |

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
| **30-day return window** | Return validates `DeliveredAt` within 30 days; testable (injectable date or equivalent) | 10 = all correct, <7 = needs time pattern |
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
# Should be 14

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
| L1: Structural | 18 | 0–18 |
| L2: Behavioral | 13 | 0–13 |
| L3: Architecture & API | 12 | 0–12 |
| L4: Tests | 9 | 0–9 |
| L5: Feedback | 4 | 0–4 |
| L6: Feature Addition | 10 | 0–10 |
| **Total** | **66** | **0–66** |

### Step 5: Record in Tracking Table

| Date | Trellis Version | AI Model | L1 (/18) | L2 (/13) | L3 (/12) | L4 (/9) | L5 (/4) | L6 (/10) | Total (/66) | Notes |
|------|----------------|----------|----------|---------|----------|---------|---------|----------|-------------|-------|
| | | | | | | | | | | |

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

**Total score of 60+ out of 66** — meaning at least 60 of the 66 criteria achieve 7+/10 consistency across independent AI runs.

The Level 6 criteria specifically measure whether the architecture and copilot instructions support **incremental change** — the most important real-world capability. A model that scores 56/56 on L1–L5 but can't modify the codebase without regressions isn't production-ready.

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
| `Trellis.EntityFrameworkCore` | ApplyTrellisConventions, SaveChangesResultAsync, FirstOrDefaultMaybeAsync, .Where(spec) |
| `Trellis.Testing` | `.Should().BeSuccess()`, `.Should().BeFailure()`, `.Should().BeFailureOfType<T>()`, `.Should().HaveValue()`, `.Should().BeNone()`, `FakeRepository`, `ResultBuilder`, `ValidationErrorBuilder` |
