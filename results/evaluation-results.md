# Trellis AI Evaluation Results

Tracks how well different AI models implement the Order Management spec using Trellis conventions.

**Evaluation spec:** Order Management (see `specs/order-management.md`)
**Scoring framework:** 57 criteria across 5 levels (see `trellis-training-lab.md`) *(was 58 prior to alpha.104 — removed SLI per-controller, replaced Migration with EnsureCreated)*
**Goal:** Total score of 52+/57

---

## Summary Table

> **Note:** Runs prior to alpha.104 used 58 criteria (L3 had 14 items including SLI per-controller and Migration). Starting with alpha.104, L3 has 13 items (removed SLI per-controller, replaced Migration with EnsureCreated), for 57 total. Scores are not directly comparable across criteria versions.

| Date | AI Model | Trellis | Template | Build | Tests | L1 (/18) | L2 (/13) | L3 | L4 (/9) | L5 (/4) | Total | Verdict |
|------|----------|---------|----------|-------|-------|----------|----------|----|---------|---------|-------|---------|
| 2025-07-18 | Claude Sonnet 4 (Copilot) | alpha.98 | 1.0.4-alpha | 0 errors | 125/125 | 5/18 | 5/13 | 10/14 | 6/9 | 0/4 | **26/58** | **FAIL** |
| 2026-03-03 | Gemini 2.5 Pro (Copilot) | alpha.98 | 1.0.4-alpha | 49 errors | 0/0 (no build) | 11/18 | 11/13 | 10/14 | 0/9 | 4/4 | **36/58** | **FAIL** |
| 2026-03-03 | Gemini 2.5 Pro Run 2 (Copilot) | alpha.98 | 1.0.4-alpha | 43 errors | 0/0 (no build) | 12/18 | 10/13 | 11/14 | 0/9 | 3/4 | **36/58** | **FAIL** |
| 2026-03-03 | GPT-5.2 Codex Max (Copilot) | alpha.98 | 1.0.4-alpha | 0 errors | 26/26 (template only) | 16/18 | 12/13 | 12/14 | 0/9 | 0/4 | **40/58** | **FAIL** |
| 2026-03-03 | Claude Opus 4.6 (Copilot) | alpha.98 | 1.0.4-alpha | 0 errors | 114/114 | 18/18 | 12/13 | 12/14 | 9/9 | 4/4 | **55/58** | **PASS** |
| 2026-03-03 | Claude Sonnet 4.6 Run 2 (Copilot) | alpha.98 | 1.0.4-alpha | 0 errors | 111/111 | 17/18 | 12/13 | 13/14 | 9/9 | 4/4 | **55/58** | **PASS** |
| 2026-03-03 | Claude Opus 4.6 Run 2 (Copilot) | alpha.99 | 1.0.5-alpha | 0 errors | 146/146 | 18/18 | 12/13 | 13/14 | 8/9 | 4/4 | **55/58** | **PASS** |
| 2026-03-10 | Claude Opus 4.6 Run 4 (Copilot) | alpha.104 | 1.0.3-alpha | 0 errors | 74/75 (1 fail) | 18/18 | 13/13 | 13/13 | 5/9 | 4/4 | **53/57** | **PASS** |

---

## Detailed Scorecard: Claude Sonnet 4 (Copilot)

**Date:** 2025-07-18
**Model:** Claude Sonnet 4 (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 0 errors, 0 warnings
**Test result:** 125/125 (52 Domain + 22 Application + 26 Acl + 25 API)
**Output location:** `C:\github\xavier\OrderManagement-Sonnet4\OrderManagement`

### Level 1: Structural Consistency — 5/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **FAIL** | Missing Money, FirstName, LastName, ProductName, Quantity — uses raw primitives |
| 2 | Value objects use TryCreate | **FAIL** | Only 5 TryCreate methods; missing ones use raw types |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order all extend Aggregate\<TId\> |
| 4 | Line items are entities | **FAIL** | LineItem extends ValueObject instead of Entity\<LineItemId\> |
| 5 | State machine uses Stateless | **FAIL** | No Trellis.Stateless package; hand-coded if/else transitions |
| 6 | State transitions return Result | **PASS** | Transitions return Result\<Order\> (but without Stateless machinery) |
| 7 | Domain events defined | **FAIL** | Zero domain events — no DomainEvent types anywhere |
| 8 | Specification exists | **FAIL** | OverdueOrderSpecification is a static class, not Specification\<Order\> |
| 9 | CQRS pattern used | **PASS** | 14 commands/queries with handlers via Trellis.Mediator |
| 10 | Authorization on commands | **FAIL** | Zero IAuthorize implementations; rolled own Actor/IActorProvider |
| 11 | Permissions as constants | **FAIL** | No Permissions class; authorization is ad-hoc |
| 12 | Repository interfaces in Application | **PASS** | 3 repository interfaces correctly in Application layer |
| 13 | EF Core in Acl (T) | **PASS** | DbContext and repos in Acl (template preserved) |
| 14 | ApplyTrellisConventions used (T) | **FAIL** | 0 usages; 10× HasConversion instead — **broke template pattern** |
| 15 | Project structure matches template (T) | **PASS** (partial) | 4-project structure preserved |
| 16 | No exceptions for control flow | **FAIL** | 1 try/catch in Domain/Application layers |
| 17 | build/test.props shared (T) | **PASS** | Template preserved |
| 18 | No primitive obsession | **FAIL** | Uses raw string for names, int for quantity, decimal for money |

**Critical failures:** No Stateless state machine, no IAuthorize/IAuthorizeResource, no domain events, massive primitive obsession, broke ApplyTrellisConventions.

### Level 2: Behavioral Consistency — 5/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Stock check present before submit |
| 2 | Cancel releases stock | **PASS** | Returns `(Order, stockReleases)` tuple for handler to process |
| 3 | Line item price snapshot | **PASS** | UnitPrice captured at line item creation |
| 4 | Duplicate product in order | **PASS** | "already in this order" validation present |
| 5 | Last line item protection | **PASS** | `_lineItems.Count <= 1` check in RemoveLineItem |
| 6 | Error types match | **FAIL** | Uses ValidationError but missing proper ConflictError/ForbiddenError taxonomy |
| 7 | Order total computed | **PASS** (partial) | LineItem.Total computed; order-level total not verified |
| 8 | Overdue spec correct | **FAIL** | Static class, not Specification\<Order\>; logic correct but not composable |
| 9 | IDs use RequiredGuid with V7 | **FAIL** | Only 4 RequiredGuid usages; should use V7 everywhere |
| 10 | Maybe for optional phone | **FAIL** | Zero Maybe\<T\> usage; phone likely nullable string |
| 11 | ParallelAsync for draft order | **FAIL** | Zero ParallelAsync/WhenAll; sequential fetches |
| 12 | Cancel resource auth check | **FAIL** | No IAuthorizeResource; no owner-vs-admin check |
| 13 | SaveChangesResultAsync used | **FAIL** | Zero usages; uses bare SaveChangesAsync |

**Note:** Business logic is mostly correct but implemented with wrong patterns.

### Level 3: Architecture & API Consistency — 10/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects, correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis.DomainDrivenDesign, Primitives, Results |
| 3 | Pipeline behaviors registered | **FAIL** | Zero pipeline behaviors; no Trellis.Mediator behaviors |
| 4 | IActorProvider registered | **FAIL** | Rolled own Actor/IActorProvider instead of Trellis.Authorization |
| 5 | DI extension per layer | **PASS** | DI extensions present |
| 6 | Endpoint paths match | **PASS** | 14 HTTP endpoints present |
| 7 | API versioning configured | **PASS** | Asp.Versioning configured (template) |
| 8 | SLI on every controller | **PASS** | 14 SLI attribute usages |
| 9 | Problem Details for errors | **PASS** | ProblemDetails used (6 references) |
| 10 | 201 for creation with Location | **PASS** | CreatedAtAction for POST endpoints |
| 11 | Health check endpoint | **PASS** | Health checks present (template) |
| 12 | DTOs in Api layer | **PASS** | Models/contracts in Api layer |
| 13 | EF Core entity configurations | **PASS** | 3 IEntityTypeConfiguration classes |
| 14 | Migration exists | **FAIL** | No migrations (using EnsureCreated — acceptable for lab but criterion says migration required) |

### Level 4: Test Consistency — 6/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 52 domain tests |
| 2 | Happy path tests | **PASS** | Present |
| 3 | Error path tests | **PASS** | Present |
| 4 | State machine tests | **PASS** | Transitions tested (even though no actual state machine) |
| 5 | Specification test | **FAIL** | No Specification\<Order\> to test; static class tested differently |
| 6 | Authorization tests | **FAIL** | No Trellis authorization to validate |
| 7 | Maybe assertion tests | **FAIL** | No Maybe\<T\> used, so no HaveValue/BeNone assertions |
| 8 | API integration tests | **PASS** | 25 API integration tests |
| 9 | Trellis.Testing used | **PASS** | 71 BeSuccess/BeFailure assertions |

### Level 5: Feedback Quality — 0/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **FAIL** | No TRELLIS_FEEDBACK.md generated |
| 2 | Friction points specific | **FAIL** | N/A — no file |
| 3 | What Worked Well present | **FAIL** | N/A — no file |
| 4 | Copilot instructions feedback | **FAIL** | N/A — no file |

### Overall Assessment

**Score: 26/58 (45%) — FAIL (target: 53+/58)**

The model produced **working code** (125 passing tests, 0 build errors) but **largely ignored Trellis conventions**. It treated the Trellis packages as optional suggestions rather than required building blocks.

**Key pattern failures:**
- **No Trellis.Stateless** — hand-coded if/else state transitions instead of state machine
- **No Trellis.Authorization** — rolled its own Actor/IActorProvider instead of using the framework's
- **No pipeline behaviors** — no ValidationBehavior, AuthorizationBehavior, ResourceAuthorizationBehavior
- **No Trellis.EntityFrameworkCore** — used HasConversion × 10 instead of ApplyTrellisConventions
- **Massive primitive obsession** — raw string, int, decimal throughout domain instead of value objects
- **No domain events** — completely missing event-driven patterns
- **No Maybe\<T\>** — nullable types instead of functional optionals
- **No feedback file** — ignored the requirement to produce TRELLIS_FEEDBACK.md

**What it got right:**
- Clean architecture layers and dependency direction
- CQRS with Mediator commands/queries
- Core business logic (stock validation, cancel releases, duplicate prevention, last item protection)
- Trellis.Testing assertions (71 usages)
- Template infrastructure preserved (build files, versioning, SLI, health checks)

**Verdict:** Claude Sonnet 4 is **not capable** of reliably producing Trellis-idiomatic code. It understood the business domain but defaulted to generic .NET patterns instead of following the copilot instructions and API reference.

---

## Model Capability Summary

| AI Model | Score | Build | Tests | Trellis Conventions | Verdict |
|----------|-------|-------|-------|---------------------|---------|
| Claude Opus 4.6 (Copilot) | 55/58 | 0 errors | 114/114 | Near-perfect adherence, 8 friction points documented | **CAPABLE** |
| Claude Opus 4.6 Run 2 (Copilot) | 55/58 | 0 errors | 146/146 | Near-perfect adherence, 7 friction points documented. Improved: IEntityTypeConfiguration classes. No spec unit test. | **CAPABLE** |
| Claude Opus 4.6 Run 4 (Copilot) | 53/57† | 0 errors | 74/75 | Perfect L1-L3 + first L2 13/13. ParallelAsync used. L4 regressed to 5/9 (test coverage gaps) | **CAPABLE** |
| Claude Sonnet 4.6 Run 2 (Copilot) | 55/58 | 0 errors | 111/111 | Near-perfect adherence, 9 friction points documented. No Bind API usage. | **CAPABLE** |
| Claude Opus 4 (Copilot) | ~50/58* | 0 errors | 82/82 | Strong adherence, 4 minor friction points | **CAPABLE** |
| GPT-5.2 Codex Max (Copilot) | 40/58 | 0 errors | 26/26 | Best architecture of non-Opus models, zero tests and feedback | **NOT CAPABLE** |
| Gemini 2.5 Pro (Copilot) | 36/58 | 49 errors | 0/0 | Read instructions well, hallucinated API surface | **NOT CAPABLE** |
| Gemini 2.5 Pro Run 2 (Copilot) | 36/58 | 43 errors | 0/0 | Consistent with Run 1 — same hallucination pattern | **NOT CAPABLE** |
| Claude Sonnet 4 (Copilot) | 26/58 | 0 errors | 125/125 | Ignored most conventions | **NOT CAPABLE** |

\* Opus 4 score is estimated from prior evaluation; formal 58-criterion scoring not performed at the time.
† Run 4 uses alpha.104 criteria (57-point scale); not directly comparable to 58-point runs.

---

## Detailed Scorecard: Gemini 2.5 Pro (Copilot)

**Date:** 2026-03-03
**Model:** Gemini 2.5 Pro (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 49 errors, 0 warnings
**Test result:** N/A — does not compile
**Output location:** `C:\github\xavier\OrderManagement-Gemini25Pro\OrderManagement`

### Level 1: Structural Consistency — 11/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **FAIL** | Missing Money, ProductName, Quantity — has 8/11 |
| 2 | Value objects use TryCreate | **PASS** | 14 TryCreate methods |
| 3 | Aggregates inherit correctly | **FAIL** | Uses hallucinated `AggregateRoot<>` instead of `Aggregate<>` |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` — correct! |
| 5 | State machine uses Stateless | **FAIL** | No Trellis.Stateless; hand-coded if/else |
| 6 | State transitions return Result | **PASS** | Returns Result from transitions |
| 7 | Domain events defined | **PASS** | 13 DomainEvent references, events present |
| 8 | Specification exists | **PASS** | `OverdueOrdersSpecification : Specification<Order>` — correct! |
| 9 | CQRS pattern used | **PASS** | 14 commands/queries with handlers |
| 10 | Authorization on commands | **PASS** | 18 IAuthorize hits; IAuthorizeResource on CancelOrder |
| 11 | Permissions as constants | **FAIL** | No Permissions class found |
| 12 | Repository interfaces in Application | **PASS** | 3 repo interfaces in Application |
| 13 | EF Core in Acl (T) | **PASS** | Template preserved |
| 14 | ApplyTrellisConventions used (T) | **PASS** | 2 usages (also 1 HasConversion) |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved |
| 16 | No exceptions for control flow | **FAIL** | 1 try/catch found |
| 17 | build/test.props shared (T) | **PASS** | Template preserved |
| 18 | No primitive obsession | **FAIL** | Missing Money/ProductName/Quantity means raw types used |

### Level 2: Behavioral Consistency — 11/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `reserveStock` function passed to Submit |
| 2 | Cancel releases stock | **PASS** | `releaseStock` function; releases per line item |
| 3 | Line item price snapshot | **PASS** | UnitPrice captured on LineItem at creation |
| 4 | Duplicate product in order | **PASS** | `ValidationErrors.Duplicate("LineItem")` |
| 5 | Last line item protection | **PASS** | `_lineItems.Count <= 1` check |
| 6 | Error types match | **PASS** | Uses ValidationErrors helper — correct taxonomy |
| 7 | Order total computed | **PASS** | `TotalPrice => UnitPrice * Quantity.Value` on LineItem |
| 8 | Overdue spec correct | **PASS** | Specification<Order> with correct logic |
| 9 | IDs use RequiredGuid with V7 | **FAIL** | Uses hallucinated `UlidValueObject<>` instead of `RequiredGuid` |
| 10 | Maybe for optional phone | **PASS** | 8 Maybe<> references |
| 11 | ParallelAsync for draft order | **FAIL** | 0 ParallelAsync usages |
| 12 | Cancel resource auth check | **PASS** | `IAuthorizeResource<Order>` on CancelOrderCommand |
| 13 | SaveChangesResultAsync used | **PASS** | 4 usages |

### Level 3: Architecture & API Consistency — 10/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects, correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages |
| 3 | Pipeline behaviors registered | **FAIL** | Zero pipeline behaviors |
| 4 | IActorProvider registered | **FAIL** | Rolled own; not from Trellis.Authorization |
| 5 | DI extension per layer | **PASS** | DI extensions present |
| 6 | Endpoint paths match | **PASS** | 25 endpoints (more than required 14) |
| 7 | API versioning configured | **PASS** | 20 versioning references |
| 8 | SLI on every controller | **PASS** | 14 SLI references |
| 9 | Problem Details for errors | **PASS** | 6 ProblemDetails references |
| 10 | 201 for creation with Location | **PASS** | CreatedAtAction present |
| 11 | Health check endpoint | **PASS** | Health checks present |
| 12 | DTOs in Api layer | **PASS** | Api models/contracts used |
| 13 | EF Core entity configurations | **FAIL** | 0 IEntityTypeConfiguration classes |
| 14 | Migration exists | **FAIL** | No migrations |

### Level 4: Test Consistency — 0/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **FAIL** | Zero new test files created |
| 2 | Happy path tests | **FAIL** | No tests |
| 3 | Error path tests | **FAIL** | No tests |
| 4 | State machine tests | **FAIL** | No tests |
| 5 | Specification test | **FAIL** | No tests |
| 6 | Authorization tests | **FAIL** | No tests |
| 7 | Maybe assertion tests | **FAIL** | No tests |
| 8 | API integration tests | **FAIL** | No tests |
| 9 | Trellis.Testing used | **FAIL** | No tests |

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md present |
| 2 | Friction points specific | **PASS** | 3 FPs with category, severity, context, workaround, suggestion |
| 3 | What Worked Well present | **PASS** | Lists 5 specific Trellis features |
| 4 | Copilot instructions feedback | **PASS** | Identifies primitive VO listing gap |

### Overall Assessment

**Score: 36/58 (62%) — FAIL (target: 53+/58)**

Gemini 2.5 Pro is a fascinating contrast to Sonnet 4. It clearly **read and understood the copilot instructions** — the architectural intent is far superior. It attempted IAuthorize (18 hits), domain events (13), Specification<Order>, Entity<LineItemId>, Maybe<T>, SaveChangesResultAsync, ApplyTrellisConventions, and ToActionResult. The feedback file is excellent.

However, it **hallucinated the Trellis API surface**, producing 49 build errors:
- `Trellis.Ddd` instead of `Trellis.DomainDrivenDesign` (namespace)
- `AggregateRoot<>` instead of `Aggregate<>` (type name)
- `UlidValueObject<>` instead of `RequiredGuid` (type name)
- `StringValueObject<>`, `IntValueObject<>` (types that don't exist)
- Missing package references: Trellis.Authorization, Trellis.Mediator, Trellis.Stateless, Trellis.EntityFrameworkCore, Trellis.Testing

It also created **zero test files** — a complete L4 failure.

**Failure mode:** "Understood the instructions, hallucinated the implementation" — the opposite of Sonnet 4's "ignored the instructions, got the code working."

---

## Detailed Scorecard: Gemini 2.5 Pro Run 2 (Copilot)

**Date:** 2026-03-03
**Model:** Gemini 2.5 Pro (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 43 errors, 0 warnings
**Test result:** N/A — does not compile
**Output location:** `C:\github\xavier\OrderManagement-Gemini25Pro-Run2\OrderManagement`

### Level 1: Structural Consistency — 12/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **FAIL** | Missing Money, Quantity — has 9/11 (improved: added ProductName) |
| 2 | Value objects use TryCreate | **PASS** | 6 TryCreate methods |
| 3 | Aggregates inherit correctly | **PASS** | Uses `Aggregate<>` correctly this time (4 usages) |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` |
| 5 | State machine uses Stateless | **FAIL** | No Trellis.Stateless; hand-coded if/else |
| 6 | State transitions return Result | **PASS** | Returns Result from transitions |
| 7 | Domain events defined | **PASS** | 10 DomainEvent references; 5 RaiseDomainEvent calls |
| 8 | Specification exists | **PASS** | `Specification<Order>` present |
| 9 | CQRS pattern used | **PASS** | 14 commands/queries with handlers |
| 10 | Authorization on commands | **PASS** | 14 IAuthorize hits; IAuthorizeResource on CancelOrder |
| 11 | Permissions as constants | **FAIL** | No Permissions class |
| 12 | Repository interfaces in Application | **PASS** | 3 repo interfaces |
| 13 | EF Core in Acl (T) | **PASS** | Template preserved |
| 14 | ApplyTrellisConventions used (T) | **PASS** | 1 usage (also 2 HasConversion) |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved |
| 16 | No exceptions for control flow | **FAIL** | 1 try/catch |
| 17 | build/test.props shared (T) | **PASS** | Template preserved |
| 18 | No primitive obsession | **FAIL** | Missing Money/Quantity = raw types |

### Level 2: Behavioral Consistency — 10/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `ReserveStock` via Bind on product |
| 2 | Cancel releases stock | **PASS** | `ReleaseStock` per line item |
| 3 | Line item price snapshot | **PASS** | UnitPrice on LineItem at creation |
| 4 | Duplicate product in order | **PASS** | "already in the order" check |
| 5 | Last line item protection | **PASS** | `_lineItems.Count <= 1` |
| 6 | Error types match | **PASS** | Uses Result error taxonomy |
| 7 | Order total computed | **PASS** | Total computed from line items |
| 8 | Overdue spec correct | **PASS** | Specification<Order> with correct logic |
| 9 | IDs use RequiredGuid with V7 | **FAIL** | Used hallucinated `Id.New()` — causes CS0117 |
| 10 | Maybe for optional phone | **PASS** | 7 Maybe<> references |
| 11 | ParallelAsync for draft order | **PASS** | 3 ParallelAsync usages — improvement! |
| 12 | Cancel resource auth check | **PASS** | IAuthorizeResource present |
| 13 | SaveChangesResultAsync used | **FAIL** | Only 1 usage (should be on every repo save) |

### Level 3: Architecture & API Consistency — 11/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects, correct direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages |
| 3 | Pipeline behaviors registered | **FAIL** | Zero pipeline behaviors |
| 4 | IActorProvider registered | **FAIL** | Missing Trellis.Authorization package entirely |
| 5 | DI extension per layer | **PASS** | Present |
| 6 | Endpoint paths match | **PASS** | 14 endpoints |
| 7 | API versioning configured | **PASS** | 27 versioning references |
| 8 | SLI on every controller | **PASS** | 15 SLI references |
| 9 | Problem Details for errors | **PASS** | 7 ProblemDetails references |
| 10 | 201 for creation with Location | **PASS** | 3 CreatedAtAction usages |
| 11 | Health check endpoint | **PASS** | Present |
| 12 | DTOs in Api layer | **PASS** | Present |
| 13 | EF Core entity configurations | **PASS** | 3 IEntityTypeConfiguration classes |
| 14 | Migration exists | **FAIL** | No migrations |

### Level 4: Test Consistency — 0/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1–9 | All test criteria | **FAIL** | Zero new test files created; deleted template tests |

### Level 5: Feedback Quality — 3/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md present |
| 2 | Friction points specific | **PASS** | 3 FPs: incorrect base classes, ROP type inference, source generator |
| 3 | What Worked Well present | **PASS** | Lists project structure, Result<T>, ToActionResult |
| 4 | Copilot instructions feedback | **FAIL** | Generic "out of sync" complaint; less actionable than Run 1 |

### Overall Assessment

**Score: 36/58 (62%) — FAIL (target: 53+/58)**

Run 2 is remarkably consistent with Run 1 — same failure mode, identical score (36 vs 36). Key differences:

**Improvements from Run 1:**
- Fixed `AggregateRoot<>` → used correct `Aggregate<>` (4 usages)
- Added ProductName value object (was missing in Run 1)
- Added ParallelAsync (3 usages — Run 1 had 0)
- Added 3 IEntityTypeConfiguration classes

**Regressions from Run 1:**
- Fewer TryCreate methods (6 vs 14)
- `RaiseDomainEvent` instead of `AddDomainEvent` method name
- `Id.New()` instead of `Id.NewUniqueV7()` 
- Heavy `Result` vs `Result<T>` confusion (many CS0029 errors)
- TRLS006 analyzer errors (accessing Maybe.Value without check)
- Deleted template tests instead of extending them
- Feedback less actionable than Run 1

**Consistent failures across both runs:**
- No Trellis.Stateless state machine
- No pipeline behaviors
- No Trellis.Authorization, Mediator, EntityFrameworkCore, Testing packages
- No Permissions class
- Zero test files written
- 43–49 build errors from hallucinated API surface

**Conclusion:** Gemini 2.5 Pro consistently reads the instructions well but cannot reliably map them to correct code. The hallucination pattern is stable across runs.

---

## Detailed Scorecard: GPT-5.2 Codex Max (Copilot)

**Date:** 2026-03-03
**Model:** GPT-5.2 Codex Max (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 0 errors, 0 warnings
**Test result:** 26/26 (0 Domain + 0 Application + 26 Acl + 0 API) — only template ACL tests remain; Domain, Application, and API test runners crash with "zero tests ran"
**Output location:** `C:\github\xavier\OrderManagement-GPT52CodexMax\OrderManagement`

### Level 1: Structural Consistency — 16/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All 11 present + extras: CustomerId, OrderId, ProductId, LineItemId, Sku, ShippingAddress, FirstName, LastName, ProductName, OrderLineQuantity, StockQuantity, StockAdjustment, ActorId. Uses `EmailAddress` and `PhoneNumber` from Trellis.Primitives (not custom). Uses `Money` from Trellis.Primitives |
| 2 | Value objects use TryCreate | **PASS** | All VOs have `TryCreate` returning `Result<T>` |
| 3 | Aggregates inherit correctly | **PASS** | Customer, Product, Order all extend `Aggregate<TId>` |
| 4 | Line items are entities | **PASS** | `OrderLineItem : Entity<LineItemId>` (nested in Order) |
| 5 | State machine uses Stateless | **FAIL** | No Trellis.Stateless reference; hand-coded ROP transitions with `Ensure()` |
| 6 | State transitions return Result | **PASS** | Submit, Approve, Ship, Deliver, Cancel all return `Result<Order>` |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent — all implement `IDomainEvent` with `OccurredAt` |
| 8 | Specification exists | **FAIL** | No `OverdueOrderSpec : Specification<Order>`. Overdue logic implemented directly in `OrderRepository.ListOverdueAsync()` |
| 9 | CQRS pattern used | **PASS** | 14 commands/queries with handlers via Mediator source generator |
| 10 | Authorization on commands | **PASS** | `IAuthorize` on all commands, `IAuthorizeResource<Order>` on CancelOrderCommand with `ResourceLoaderById` |
| 11 | Permissions as constants | **PASS** | `Permissions` class with nested `Customers`, `Products`, `Orders` classes containing `const string` fields |
| 12 | Repository interfaces in Application | **PASS** | `ICustomerRepository`, `IProductRepository`, `IOrderRepository` in `Application/src/Abstractions/` |
| 13 | EF Core in Acl (T) | **PASS** | `OrderManagementDbContext` and 3 repository implementations in Acl |
| 14 | ApplyTrellisConventions used (T) | **PASS** | `configurationBuilder.ApplyTrellisConventions(...)` in ConfigureConventions — zero `HasConversion()` anywhere |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved with correct layout |
| 16 | No exceptions for control flow | **PASS** | Only try/catch is in `HttpContextActorProvider` (Api layer, for JSON parsing); Domain and Application have zero |
| 17 | build/test.props shared (T) | **PASS** | Template preserved |
| 18 | No primitive obsession | **PASS** | All domain method signatures use typed VOs. No raw Guid, string, int, decimal in domain public API |

**Excellent structural adherence.** Only missing Stateless state machine and Specification<Order> — the two most framework-specific patterns.

### Level 2: Behavioral Consistency — 12/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | Handler calls `product.ReserveStock()` per line item before `order.Submit()` |
| 2 | Cancel releases stock | **PASS** | `ReleaseAndCancelAsync` releases stock only when status is Submitted/Approved; Draft cancel skips release |
| 3 | Line item price snapshot | **PASS** | `OrderLineItem` captures `UnitPrice` (Money) at creation |
| 4 | Duplicate product in order | **PASS** | Both `TryCreate` and `AddLineItem` check for duplicate ProductId |
| 5 | Last line item protection | **PASS** | `Ensure(_ => _lineItems.Count > 1, ...)` in `RemoveLineItem` |
| 6 | Error types match | **PASS** | `ValidationError.For()`, `Error.NotFound()`, `Error.Conflict()`, `Error.Forbidden()` — correct taxonomy |
| 7 | Order total computed | **PASS** | `RecalculateTotal()` aggregates `li.GetLineTotal()` via `Money.Add()` — called on create and add/remove line item |
| 8 | Overdue spec correct | **PASS** | Logic correct: `Status == Submitted` + `submittedAt <= nowUtc.AddDays(-7)`, EF-translatable via `EF.Property<>()` |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All ID types extend `RequiredGuid<T>`, constructors use `NewUniqueV7()` |
| 10 | Maybe for optional phone | **PASS** | `Maybe<PhoneNumber>` on Customer, `MaybeProperty()` in EF config, `Maybe.From(request.PhoneNumber)` in controller |
| 11 | ParallelAsync for draft order | **FAIL** | Sequential fetches — loads customer, then loops products one by one. No `ParallelAsync` |
| 12 | Cancel resource auth check | **PASS** | `IAuthorizeResource<Order>` checks `actor.HasPermission(Cancel)` + `actor.IsOwner(order.CreatedByActorId.Value)` or admin |
| 13 | SaveChangesResultAsync used | **PASS** | `IUnitOfWork.SaveChangesAsync` returns `Task<Result<Unit>>`, implemented as `this.SaveChangesResultUnitAsync(ct)` |

**Near-perfect behavioral implementation.** Only missing `ParallelAsync` for parallel product fetching.

### Level 3: Architecture & API Consistency — 12/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects with correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Domain.csproj references only Trellis.DomainDrivenDesign, Primitives, Results, Stateless, Authorization |
| 3 | Pipeline behaviors registered | **PASS** | `services.AddMediator(...).AddTrellisBehaviors()` — validation, authorization, resource authorization all wired |
| 4 | IActorProvider registered | **PASS** | `services.AddSingleton<IActorProvider, HttpContextActorProvider>()` — reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddPresentation()`, `AddApplication()`, `AddAntiCorruptionLayer()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All 14+ endpoints with correct HTTP methods and resource-oriented paths |
| 7 | API versioning configured | **PASS** | `[ApiVersion("2026-11-12")]` on all controllers |
| 8 | SLI on every controller | **PASS** | `[ServiceLevelIndicator]` on all 3 controllers |
| 9 | Problem Details for errors | **PASS** | `ErrorHandlingMiddleware` and `AddProblemDetails()` preserved from template |
| 10 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync(this, nameof(GetById), ...)` on customer, order, and product creation |
| 11 | Health check endpoint | **PASS** | `HealthController.cs` present |
| 12 | DTOs in Api layer | **PASS** | Request/Response records in `Api/src/2026-11-12/Contracts/` — domain types not exposed |
| 13 | EF Core entity configurations | **FAIL** | All entity configuration inline in `DbContext.OnModelCreating()` — no `IEntityTypeConfiguration<T>` classes |
| 14 | Migration exists | **FAIL** | No migrations — using `EnsureCreated()` |

### Level 4: Test Consistency — 0/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **FAIL** | Deleted template `ZipCodeTests.cs`; created zero new domain tests |
| 2 | Happy path tests | **FAIL** | No tests |
| 3 | Error path tests | **FAIL** | No tests |
| 4 | State machine tests | **FAIL** | No tests |
| 5 | Specification test | **FAIL** | No tests |
| 6 | Authorization tests | **FAIL** | No tests |
| 7 | Maybe assertion tests | **FAIL** | No tests |
| 8 | API integration tests | **FAIL** | No tests — deleted template API tests |
| 9 | Trellis.Testing used | **FAIL** | No tests |

**Complete test failure.** Deleted all template tests (Domain, Application, API) and wrote zero replacements. Only the template ACL tests (26) survived because they weren't domain-related.

### Level 5: Feedback Quality — 0/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **FAIL** | No TRELLIS_FEEDBACK.md generated |
| 2 | Friction points specific | **FAIL** | N/A — no file |
| 3 | What Worked Well present | **FAIL** | N/A — no file |
| 4 | Copilot instructions feedback | **FAIL** | N/A — no file |

### Overall Assessment

**Score: 40/58 (69%) — FAIL (target: 53+/58)**

GPT-5.2 Codex Max produced the **best architectural implementation** of any model tested so far. It is the only model besides Opus 4 to achieve:
- Zero build errors
- Correct `Aggregate<TId>` (not hallucinated)
- Correct `RequiredGuid<T>` with `NewUniqueV7()`
- Correct `EmailAddress` and `PhoneNumber` from `Trellis.Primitives` (not custom)
- Correct `Money` type usage with `.Add()`, `.Multiply()` arithmetic
- `IAuthorize` + `IAuthorizeResource<Order>` with `ResourceLoaderById`
- `Permissions` class with typed constants
- `AddTrellisBehaviors()` pipeline registration
- `IActorProvider` as singleton with X-Test-Actor header
- `ApplyTrellisConventions` (no HasConversion)
- `SaveChangesResultUnitAsync` via IUnitOfWork
- `Maybe<PhoneNumber>` with `MaybeProperty()` EF config
- `DomainEvents.Add(new ...)` for all 5 events
- ROP chains with `Ensure()`, `Bind()`, `Tap()`, `Map()`

**The implementation code is genuinely excellent** — it demonstrates deep understanding of both the Trellis API and the copilot instructions.

**What killed the score:**
1. **Zero tests written (0/9)** — deleted all template tests and wrote no replacements. This alone costs 9 points.
2. **No feedback file (0/4)** — ignored the explicit prompt to create `TRELLIS_FEEDBACK.md`. Another 4 points.
3. **No Stateless state machine** — hand-coded ROP transitions (correct behavior, wrong pattern).
4. **No Specification\<Order\>** — overdue logic in repository (correct behavior, wrong abstraction).

**If GPT-5.2 Codex Max had written tests and a feedback file, it would score ~51-53/58** — potentially reaching the passing threshold. The implementation-to-test gap is its critical weakness.

**Failure mode:** "Excellent implementation, zero verification" — the opposite of what a production-ready codebase needs.

---

## Detailed Scorecard: Claude Opus 4.6 (Copilot)

**Date:** 2026-03-03
**Model:** Claude Opus 4.6 (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 0 errors, 0 warnings
**Test result:** 114/114 (66 Domain + 14 Application + 26 Acl + 8 API)
**Output location:** `C:\github\xavier\OrderManagement-Opus46\OrderManagement`

### Level 1: Structural Consistency — 18/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All 11+ present: OrderId, CustomerId, ProductId, LineItemId (RequiredGuid), Sku (RequiredString), FirstName, LastName, ProductName, Quantity, StockQuantity, ShippingAddress (ValueObject), OrderStatus (RequiredEnum). Uses EmailAddress, PhoneNumber, Money from Trellis.Primitives |
| 2 | Value objects use TryCreate | **PASS** | All VOs have `TryCreate` returning `Result<T>`. `Create` convenience methods on simpler types |
| 3 | Aggregates inherit correctly | **PASS** | `Customer : Aggregate<CustomerId>`, `Product : Aggregate<ProductId>`, `Order : Aggregate<OrderId>` |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` — correct identity-based entity |
| 5 | State machine uses Stateless | **PASS** | **First model to use Trellis.Stateless!** `StateMachine<string, string>` with lazy init pattern to work with EF Core parameterless constructor. Uses `FireResult()` for all transitions |
| 6 | State transitions return Result | **PASS** | All transitions (Submit, Approve, Ship, Deliver, Cancel) return `Result<Order>` via `StateMachine.FireResult()` |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent — all implement `IDomainEvent` with `DomainEvents.Add()` |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with `ToExpression()` — EF-translatable. Used in `OrderRepository.GetOverdueOrdersAsync()` via `.Where(new OverdueOrderSpecification())` |
| 9 | CQRS pattern used | **PASS** | 14+ commands/queries with handlers via Mediator source generator: CreateDraftOrder, Submit, Approve, Ship, Deliver, Cancel, AddLineItem, RemoveLineItem, GetOrderById, ListOrdersByCustomer, ListOverdueOrders, CreateCustomer, CreateProduct, AddStock |
| 10 | Authorization on commands | **PASS** | `IAuthorize` on all commands with `RequiredPermissions`. `IAuthorizeResource<Order>` on CancelOrderCommand with `ResourceLoaderById` and owner-or-admin check via `CancelOrderResourceLoader` |
| 11 | Permissions as constants | **PASS** | `Permissions` static class with `const string` fields: OrdersCreate, OrdersSubmit, OrdersApprove, OrdersShip, OrdersDeliver, OrdersCancel, OrdersRead, OrdersReadAll, CustomersCreate, ProductsCreate, ProductsManageStock |
| 12 | Repository interfaces in Application | **PASS** | `ICustomerRepository`, `IProductRepository`, `IOrderRepository` in `Application/src/Abstractions/` |
| 13 | EF Core in Acl (T) | **PASS** | `OrderManagementDbContext` and 3 sealed repository implementations in `Acl/src/Repositories/` |
| 14 | ApplyTrellisConventions used (T) | **PASS** | `configurationBuilder.ApplyTrellisConventions(...)` in `ConfigureConventions`. One `HasConversion` for `OrderStatus` (RequiredEnum → string) which ApplyTrellisConventions may not auto-handle — acceptable workaround |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved: Domain, Application, Acl, Api with correct src/tests separation |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain or Application layers. Only `ErrorHandlingMiddleware` in Api (template code) |
| 17 | build/test.props shared (T) | **PASS** | Template build infrastructure preserved |
| 18 | No primitive obsession | **PASS** | All domain public APIs use typed VOs. `CreatedByActorId` is `string` which mirrors `Actor.Id` from the framework — a boundary concern, not primitive obsession |

**Perfect structural score.** First model to achieve 18/18. First to use Stateless state machine and Specification\<Order\>.

### Level 2: Behavioral Consistency — 12/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `Submit(Func<ProductId, int, Result<Unit>> reserveStock)` — stock reservation delegate called per line item, rolls back on failure |
| 2 | Cancel releases stock | **PASS** | `Cancel(Action<ProductId, int>? releaseStock)` — releases stock per line item only when previous status was Submitted or Approved |
| 3 | Line item price snapshot | **PASS** | `LineItem.TryCreate(productId, productName, quantity, unitPrice)` — captures UnitPrice at creation |
| 4 | Duplicate product in order | **PASS** | `AddLineItem` checks `_lineItems.Any(li => li.ProductId == lineItem.ProductId)` — rejects duplicate products |
| 5 | Last line item protection | **PASS** | `RemoveLineItem` checks `_lineItems.Count > 1` — prevents removing last item |
| 6 | Error types match | **PASS** | Uses `Error.Validation()`, `Error.NotFound()` taxonomy consistently |
| 7 | Order total computed | **PASS** | `OrderTotal => _lineItems.Sum(li => li.LineTotal)` — computed property aggregating line totals |
| 8 | Overdue spec correct | **PASS** | `OverdueOrderSpecification` checks `Status == Submitted` and `SubmittedAt <= 7 days ago` — EF-translatable expression |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All ID types extend `RequiredGuid<T>`, constructors use `NewUniqueV7()` |
| 10 | Maybe for optional phone | **PASS** | `Maybe<PhoneNumber>` on Customer with `_phoneNumber` backing field, `MaybeProperty()` in EF config, `Maybe.From()`/`Maybe.None<>()` in domain |
| 11 | ParallelAsync for draft order | **FAIL** | Sequential fetches in CreateDraftOrderHandler — loads customer then products one by one. No `ParallelAsync` usage anywhere |
| 12 | Cancel resource auth check | **PASS** | `IAuthorizeResource<Order>` on CancelOrderCommand with `CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>` — checks owner-or-admin |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync(ct)` in all 3 repositories — consistent Result\<Unit\> return |

**Near-perfect behavioral implementation.** Only `ParallelAsync` missing for parallel product fetching in draft order creation.

### Level 3: Architecture & API Consistency — 12/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects with correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages in Domain.csproj |
| 3 | Pipeline behaviors registered | **PASS** | `services.AddMediator(...).AddTrellisBehaviors()` — validation, authorization, resource authorization all wired |
| 4 | IActorProvider registered | **PASS** | `services.AddSingleton<IActorProvider, TestActorProvider>()` — reads X-Test-Actor header, parses JSON to `Actor.Create(id, permissions)` |
| 5 | DI extension per layer | **PASS** | `AddDomainServices()`, `AddApplicationServices()`, `AddAntiCorruptionLayer()`, `AddPresentationServices()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All 14+ endpoints present: POST orders, submissions, approvals, shipments, deliveries, cancellations, customers, products, stock-additions; GET orders/{id}, customers/{id}/orders, orders/overdue, health |
| 7 | API versioning configured | **PASS** | `[ApiVersion("2026-11-12")]` on all 3 controllers + template weather controller |
| 8 | SLI on every controller | **PASS** | `[ServiceLevelIndicator]` on OrdersController, CustomersController, ProductsController |
| 9 | Problem Details for errors | **PASS** | `ErrorHandlingMiddleware` + `AddProblemDetails()` preserved from template |
| 10 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync(this, nameof(GetById), ...)` on customer, order, product creation |
| 11 | Health check endpoint | **PASS** | `app.MapHealthChecks("/health")` in Program.cs |
| 12 | DTOs in Api layer | **PASS** | Request/Response sealed classes in `Api/src/Contracts/` with `MappingExtensions` — domain types not exposed |
| 13 | EF Core entity configurations | **FAIL** | All entity configuration inline in `DbContext.OnModelCreating()` — no `IEntityTypeConfiguration<T>` classes. Configuration works but is not separated into per-entity files |
| 14 | Migration exists | **FAIL** | No migrations — using `EnsureCreated()` from template. Acceptable for lab but criterion requires migration |

### Level 4: Test Consistency — 9/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 66 domain tests across 8 files: OrderTests, CustomerTests, ProductTests, OverdueOrderSpecificationTests, QuantityTests, ShippingAddressTests, SkuTests, StockQuantityTests |
| 2 | Happy path tests | **PASS** | Complete coverage: create/submit/approve/ship/deliver flows, customer creation with/without phone, product creation, stock add/reserve/release, specification matching |
| 3 | Error path tests | **PASS** | Invalid transitions (approve draft, cancel delivered), nonexistent entities, insufficient stock, empty line items, negative price, duplicate email/SKU |
| 4 | State machine tests | **PASS** | All 5 transitions tested forward + invalid transitions: `Submit_draft_order_succeeds`, `Approve_submitted_order_succeeds`, `Ship_approved_order_succeeds`, `Deliver_shipped_order_succeeds`, `Approve_draft_order_fails`, `Cancel_delivered_order_fails` |
| 5 | Specification test | **PASS** | `OverdueOrderSpecificationTests` with overdue scenario (submitted 8 days ago) and non-overdue scenario (submitted 1 day ago). Uses `Specification.IsSatisfiedBy()` |
| 6 | Authorization tests | **PASS** | Application handler tests use `ISender` through full Mediator pipeline with `AddTrellisBehaviors()`. API integration tests exercise authorization via X-Test-Actor header with explicit permissions. Validation behavior confirmed working by `CreateDraftOrder_with_empty_line_items_fails_validation` test |
| 7 | Maybe assertion tests | **PASS** | `CustomerTests` verify `Maybe<PhoneNumber>` in both scenarios: `HaveValueMatching(c => !c.PhoneNumber.HasValue)` for None and `HaveValueMatching(c => c.PhoneNumber.HasValue)` for Some via `Maybe.From(phone)` |
| 8 | API integration tests | **PASS** | 8 integration tests: full order lifecycle (create→submit→approve→ship→deliver), cancel with stock release, nonexistent customer (404), health check. Uses `TestWebApplicationFactory` with real HTTP pipeline |
| 9 | Trellis.Testing used | **PASS** | `BeSuccess()`, `BeFailure()`, `HaveValueMatching()` assertions throughout all layers. FluentAssertions with Trellis extensions on every result assertion |

**Perfect test score.** First model to achieve 9/9. 114 tests covering domain, application, and API integration layers. Template tests preserved (26 ACL + template domain/API tests).

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | `TRELLIS_FEEDBACK.md` — 129 lines, the most comprehensive feedback of any model |
| 2 | Friction points specific | **PASS** | 8 detailed friction points with Category, Severity, Context, What Happened, Workaround, Suggested Improvement: FP-1 (Trellis.Unit vs Mediator.Unit ambiguity), FP-2 (TRLS003/004 analyzer strictness), FP-3 (Task vs ValueTask overload ambiguity), FP-4 (Stateless NullReferenceException with EF Core — HIGH), FP-5 (ResourceAuthorizationBehavior singleton/scoped conflict — HIGH), FP-6 (Money.Create throws instead of Result), FP-7 (Actor.Create discovery), FP-8 (Maybe\<T\> EF Core ceremony) |
| 3 | What Worked Well present | **PASS** | 10 specific items: value object source generation, ApplyTrellisConventions, Result\<T\> ROP chains, ToActionResult, analyzers, Specification pattern, RequiredEnum, SaveChangesResultAsync, Trellis.Testing, AddScalarValueValidation |
| 4 | Copilot instructions feedback | **PASS** | 4 specific copilot instructions improvements: scoped IResourceLoader note, Money throws vs Result inconsistency, Actor.Create documentation, lazy Stateless pattern for EF Core |

**Perfect feedback score.** 3 suggested new features (LazyStateMachine wrapper, Result-based Money.TryCreate, convention-based Maybe EF mapping). Two HIGH-severity friction points (FP-4 and FP-5) identified real framework issues.

### Overall Assessment

**Score: 55/58 (95%) — PASS (target: 53+/58)**

Claude Opus 4.6 is the **first model to pass the evaluation threshold** and produced the most Trellis-idiomatic implementation of any model tested. It is the first and only model to:

- **Use Trellis.Stateless state machine** with lazy initialization pattern for EF Core compatibility
- **Use Specification\<Order\>** for overdue order detection, composable with EF Core `.Where()`
- **Score 18/18 on L1** — perfect structural consistency
- **Score 9/9 on L4** — perfect test consistency with 114 passing tests
- **Score 4/4 on L5** — most comprehensive feedback of any model (8 friction points, 10 WWW items, 3 new feature suggestions)
- **Preserve all template tests** — did not delete any template baseline tests
- **Write meaningful domain tests** — 66 domain tests covering aggregates, entities, value objects, specifications
- **Write application handler tests** — 14 tests with NSubstitute mocks going through full Mediator pipeline
- **Write API integration tests** — 8 tests covering full lifecycle, cancellation, error cases, health check
- **Produce actionable framework feedback** — identified 2 HIGH-severity real bugs (Stateless/EF Core interaction, singleton/scoped ResourceAuthorizationBehavior)

**What it missed (3 points):**
1. **No ParallelAsync** (L2-11) — sequential product fetches in CreateDraftOrderHandler instead of `Result.ParallelAsync()`
2. **No IEntityTypeConfiguration** (L3-13) — inline entity config in DbContext.OnModelCreating instead of per-entity configuration classes
3. **No migration** (L3-14) — using `EnsureCreated()` from template; no `dotnet ef migrations add` performed

**Minor observations (not scored against):**
- `CreatedByActorId` is `string` rather than a typed ActorId VO — mirrors framework's `Actor.Id` type
- DTOs are `sealed class` with mutable properties rather than `sealed record` — functional but less idiomatic C#
- Domain aggregates not `sealed` — not a scored criterion but best practice
- `SubmitOrderHandler` uses `.GetAwaiter().GetResult()` to bridge sync `Func<>` delegate with async repository — pragmatic workaround for domain constraint
- `Contracts/` folder is at `Api/src/Contracts/` rather than `Api/src/2026-11-12/Contracts/` — minor organizational difference
- `OrderTotal` uses `decimal` (`.Sum(li => li.LineTotal)`) rather than `Money` type — computed aggregate suitable as decimal

**Verdict:** Claude Opus 4.6 is **CAPABLE** of producing Trellis-idiomatic code. It demonstrated deep understanding of the framework, produced zero build errors, wrote comprehensive tests at all layers, preserved template patterns, and generated the most actionable framework feedback of any model tested.

---

## Detailed Scorecard: Claude Sonnet 4.6 Run 2 (Copilot)

**Date:** 2026-03-03
**Model:** Claude Sonnet 4.6 (via GitHub Copilot agent mode) — second attempt
**Trellis version:** 3.0.0-alpha.98
**Template version:** 1.0.4-alpha
**Build result:** 0 errors, 0 warnings
**Test result:** 111/111 (40 Domain + 27 Application + 26 Acl + 18 API)
**Output location:** `C:\github\xavier\OrderManagement-Sonnet46-Run2\OrderManagement`

### Level 1: Structural Consistency — 17/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All 11+ present: OrderId, CustomerId, ProductId, LineItemId (RequiredGuid), Sku (ScalarValueObject with regex), FirstName, LastName, ProductName (ScalarValueObject with IParsable), Quantity (ScalarValueObject<int>), ShippingAddress (ValueObject), OrderStatus (RequiredEnum). Uses EmailAddress, PhoneNumber from Trellis.Primitives, Money from Trellis.Primitives |
| 2 | Value objects use TryCreate | **PASS** | All VOs have `TryCreate` returning `Result<T>` with proper validation |
| 3 | Aggregates inherit correctly | **PASS** | `Customer : Aggregate<CustomerId>`, `Product : Aggregate<ProductId>`, `Order : Aggregate<OrderId>` — all `sealed` classes |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` — correct identity-based entity, sealed |
| 5 | State machine uses Stateless | **PASS** | **Second model ever to use Trellis.Stateless!** `StateMachine<OrderStatus, OrderTrigger>` with enum triggers and `FireResult()`. Creates new machine instance in each method call (stateless pattern, no caching) |
| 6 | State transitions return Result | **PASS** | All transitions return `Result<Order>` via `FireResult().Tap(...).Map(...)` chains |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent — all `sealed record` implementing `IDomainEvent` |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with `ToExpression()` — injectable `DateTime` for testability |
| 9 | CQRS pattern used | **PASS** | All operations as Commands/Queries with Handlers via Mediator source generator: CreateDraftOrder, Submit, Approve, Ship, Deliver, Cancel, AddLineItem, RemoveLineItem, GetOrderById, ListOrdersByCustomer, ListOverdueOrders, CreateCustomer, CreateProduct, AddStock |
| 10 | Authorization on commands | **PASS** | `IAuthorize` on all commands with `RequiredPermissions`. `IAuthorizeResource<Order>` on CancelOrderCommand with `CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>` |
| 11 | Permissions as constants | **PASS** | `Permissions` class with `const string` fields + `All` set for test convenience |
| 12 | Repository interfaces in Application | **PASS** | `ICustomerRepository`, `IProductRepository`, `IOrderRepository` in `Application/src/Abstractions/` |
| 13 | EF Core in Acl (T) | **PASS** | `OrderManagementDbContext` and 3 sealed repository implementations in `Acl/src/Repositories/` |
| 14 | ApplyTrellisConventions used (T) | **PASS** | `ApplyTrellisConventions` in `ConfigureConventions`. Zero `HasConversion()` calls anywhere |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved with correct src/tests separation |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain or Application layers. Only `ErrorHandlingMiddleware` in Api (template code) |
| 17 | build/test.props shared (T) | **PASS** | Template build infrastructure preserved |
| 18 | No primitive obsession | **FAIL** | `Product.StockQuantity` is raw `int` and `AddStock(int quantity)` takes a raw int parameter. No `StockQuantity` value object. `ReserveStock(Quantity)` and `ReleaseStock(Quantity)` correctly use typed VOs, but AddStock bypasses this |

**Near-perfect structural score.** Only missed StockQuantity value object — a subtle primitive obsession case since the property and one method use raw int while other stock methods correctly use typed Quantity.

### Level 2: Behavioral Consistency — 12/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `SubmitOrderHandler` fetches products via `GetByIdsAsync()` (batch), calls `product.ReserveStock(quantity)` per line item, saves all products with `SaveManyAsync()` |
| 2 | Cancel releases stock | **PASS** | `CancelOrderHandler` releases stock per line item when cancelling a submitted/approved order. Application test explicitly verifies stock release after cancel |
| 3 | Line item price snapshot | **PASS** | `LineItem.Create(productId, productName, quantity, unitPrice)` — captures UnitPrice at creation from product catalog |
| 4 | Duplicate product in order | **PASS** | Both `Order.TryCreate` and `AddLineItem` check for duplicate ProductId — returns `ValidationError` |
| 5 | Last line item protection | **PASS** | `RemoveLineItem` checks `_lineItems.Count <= 1` — prevents removing last item with `ValidationError` |
| 6 | Error types match | **PASS** | Uses `ValidationError`, `NotFoundError`, `ConflictError`, `ForbiddenError` consistently per spec. Tests verify error types with `BeOfType<>()` |
| 7 | Order total computed | **PASS** | `CalculateTotal()` returns `Money` — uses `Money.Multiply(quantity)` + `Money.Add()` with `.Match()` pattern |
| 8 | Overdue spec correct | **PASS** | `OverdueOrderSpecification` checks `Status == Submitted` + `SubmittedAtUtc < threshold` (7-day) — EF-translatable expression |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All ID types extend `RequiredGuid<T>`, use `NewUniqueV7()` |
| 10 | Maybe for optional phone | **PASS** | `Maybe<PhoneNumber>` on Customer with `_phoneNumber` backing field, `MaybeProperty()` in EF config, `Maybe.From()`/`Maybe.None<>()` in domain |
| 11 | ParallelAsync for draft order | **FAIL** | Sequential fetches — `CreateDraftOrderHandler` loads customer, then products via `GetByIdsAsync` (batch but sequential). No `ParallelAsync` |
| 12 | Cancel resource auth check | **PASS** | `IAuthorizeResource<Order>` on CancelOrderCommand with `CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>` — owner-or-admin check |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync(ct)` in all 3 repositories — consistent `Result<Unit>` return |

**Near-perfect behavioral implementation.** Only `ParallelAsync` missing — consistent with all other models tested.

### Level 3: Architecture & API Consistency — 13/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects with correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages in Domain.csproj |
| 3 | Pipeline behaviors registered | **PASS** | `services.AddMediator(...).AddTrellisBehaviors()` — validation, authorization, resource authorization all wired |
| 4 | IActorProvider registered | **PASS** | `services.AddSingleton<IActorProvider, HeaderActorProvider>()` — reads X-Test-Actor header with `IHttpContextAccessor`, JSON deserialization to `Actor.Create(id, permissions)` |
| 5 | DI extension per layer | **PASS** | Per-layer DI extensions wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All 14+ endpoints present with correct HTTP methods: POST orders, GET orders/{id}, POST orders/{id}/submission, POST orders/{id}/approval, POST orders/{id}/shipment, POST orders/{id}/delivery, POST orders/{id}/cancellation, GET orders/overdue, POST customers, GET customers/{id}/orders, POST products, POST products/{id}/stock-additions, GET health. Placeholder GET endpoints for customers/{id} and products/{id} for Location header resolution |
| 7 | API versioning configured | **PASS** | `[ApiVersion("2026-11-12")]` on all 3 controllers |
| 8 | SLI on every controller | **PASS** | `[ServiceLevelIndicator]` on OrdersController, CustomersController, ProductsController |
| 9 | Problem Details for errors | **PASS** | `ErrorHandlingMiddleware` + `AddProblemDetails()` preserved from template |
| 10 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync(this, nameof(GetCustomer/GetOrder/GetProduct), ...)` on all creation endpoints |
| 11 | Health check endpoint | **PASS** | `app.MapHealthChecks("/health")` in Program.cs |
| 12 | DTOs in Api layer | **PASS** | Sealed records in `Api/src/Contracts/` with static `From()` mapping methods — domain types not exposed |
| 13 | EF Core entity configurations | **PASS** | 4 `IEntityTypeConfiguration` classes: `OrderConfiguration`, `CustomerConfiguration`, `ProductConfiguration`, `LineItemConfiguration` — **better than Opus 4.6 and GPT-5.2** which used inline config |
| 14 | Migration exists | **FAIL** | No migrations — using `EnsureCreated()` from template |

**Excellent architecture score.** Notably, Sonnet 4.6 Run 2 is the first model to get IEntityTypeConfiguration correct with per-entity configuration classes — a criterion that both Opus 4.6 and GPT-5.2 Codex Max missed.

### Level 4: Test Consistency — 9/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 40 domain tests across OrderTests, OrderStateMachineTests, CustomerTests, ProductTests, OverdueOrderSpecificationTests (+ template ZipCodeTests preserved) |
| 2 | Happy path tests | **PASS** | Create, submit, approve, ship, deliver flows. Customer creation with/without phone. Product creation and stock management |
| 3 | Error path tests | **PASS** | Invalid transitions (submit from non-draft, approve from draft, cancel from delivered), not found, insufficient stock, empty line items, negative stock, duplicate products |
| 4 | State machine tests | **PASS** | `OrderStateMachineTests` — 13 tests covering all valid transitions (submit, approve, ship, deliver, cancel from draft/submitted/approved) and invalid transitions (cancel from delivered, approve from draft, AddLineItem on submitted) |
| 5 | Specification test | **PASS** | `OverdueOrderSpecificationTests` — 4 tests: overdue submitted, not-overdue submitted, draft never overdue, approved never overdue. Compiles `ToExpression()` |
| 6 | Authorization tests | **PASS** | Application tests send commands through full Mediator pipeline with `AddTrellisBehaviors()`. API integration tests exercise HTTP round-trip with X-Test-Actor header. `CancelOrderCommand` authorization tested in both layers |
| 7 | Maybe assertion tests | **PASS** | `CustomerTests` verify `Maybe<PhoneNumber>`: `customer.PhoneNumber.HasValue.Should().BeFalse()` for None, `customer.PhoneNumber.HasValue.Should().BeTrue()` for Some. Behavior correctly verified |
| 8 | API integration tests | **PASS** | 18 API integration tests across 3 controllers: OrderApiTests (full lifecycle, cancellation, overdue, 404 for unknown order/customer), CustomerApiTests (201 with location, 409 duplicate email, 400 invalid email, 404 unknown customer orders), ProductApiTests (201, stock additions, 404 unknown product) |
| 9 | Trellis.Testing used | **PASS** | `BeSuccess()`, `BeFailure()`, `BeFailureOfType<>()` assertions throughout all test layers. FluentAssertions with Trellis extensions used consistently |

**Perfect test score.** Second model to achieve 9/9 (after Opus 4.6). 111 tests covering domain (40), application (27), ACL (26 template), and API (18). Template tests preserved. Application tests use ISender through full Mediator pipeline — not mock-based like Opus 4.6, but real handler invocation through DI.

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | `TRELLIS_FEEDBACK.md` — comprehensive 200+ line document, the most detailed feedback of any model |
| 2 | Friction points specific | **PASS** | 9 detailed friction points: FP-1 (TRLS004 not satisfied by `!TryGetValue` — HIGH), FP-2 (AddResourceLoaders vs AddScoped — MEDIUM), FP-3 (DomainEvents protected — MEDIUM), FP-4 (CS1591 with TreatWarningsAsErrors — MEDIUM), FP-5 (PhoneNumber type ambiguity — MEDIUM), FP-6 (Singleton mock repo requirement — MEDIUM), FP-7 (Maybe.Value access pattern — LOW), FP-8 (ResourceAuthorizationBehavior singleton/scoped — HIGH), FP-9 (AddScalarValueValidation 422 vs 400 — LOW). Each has Category, Severity, Context, What Happened, Workaround, Suggested Improvement |
| 3 | What Worked Well present | **PASS** | 9 specific items: ROP with Result<T>/Map/MapAsync, ScalarValueObject generator, ApplyTrellisConventions, IAuthorize/IAuthorizeResource, IValidate, ToCreatedAtActionResult/ToActionResultAsync, FirstOrDefaultMaybeAsync/FirstOrDefaultResultAsync, SaveChangesResultAsync, Mediator source generator |
| 4 | Copilot instructions feedback | **PASS** | 6 specific improvements: IAuthorizeResource test isolation, mock repo lifetime, Trellis.Primitives imports, TRLS004 workaround documentation, ResourceAuthorizationBehavior singleton issue, AddScalarValueValidation scope clarification. Also includes 3 suggested new features (TryUnwrap helper, TestActorProvider fixture, WebApplicationFactory in-memory repo builder) |

**Perfect feedback score.** The most comprehensive feedback of any model:
- 9 friction points (vs 8 for Opus 4.6)
- Two independently identified HIGH-severity issues (TRLS004 guard pattern, ResourceAuthorizationBehavior singleton/scoped conflict — same issue Opus found)
- 3 concrete API suggestions with code examples
- 6 copilot instructions improvement items

### Overall Assessment

**Score: 55/58 (95%) — PASS (target: 53+/58)**

Claude Sonnet 4.6 Run 2 is the **second model to pass** the evaluation threshold, achieving the same score as Opus 4.6 but with a different failure profile. This represents a **dramatic improvement** from Run 1 (26/58 → 55/58), making it the largest score jump between runs of any model.

**Comparison: Sonnet 4.6 Run 2 vs Opus 4.6 (both 55/58):**

| Criterion | Sonnet 4.6 R2 | Opus 4.6 | Winner |
|-----------|---------------|----------|--------|
| No primitive obsession (L1) | **FAIL** (int StockQuantity) | **PASS** (typed StockQuantity VO) | Opus |
| ParallelAsync (L2) | **FAIL** | **FAIL** | Tie |
| EF Core entity configurations (L3) | **PASS** (4 IEntityTypeConfiguration classes) | **FAIL** (inline OnModelCreating) | **Sonnet** |
| Migration exists (L3) | **FAIL** | **FAIL** | Tie |

**Key differences in implementation approach:**
- **ROP style:** Opus uses `Bind`/`BindAsync`/`Map`/`Tap` chains in handlers. Sonnet uses imperative `if (!result.TryGetValue) { TryGetError; return; }` pattern throughout — functionally equivalent but not idiomatic ROP. This is documented in its TRELLIS_FEEDBACK.md as FP-1 and is the biggest architectural observation.
- **State machine:** Both use Stateless + FireResult. Opus uses lazy-cached `StateMachine<string, string>`. Sonnet uses `StateMachine<OrderStatus, OrderTrigger>` with enum triggers and creates new instance per method call (stateless pattern).
- **Domain events:** Both use sealed records implementing IDomainEvent. Sonnet uses record with default `OccurredAt = DateTime.UtcNow`.
- **Test approach:** Opus uses NSubstitute mocks for application tests. Sonnet uses ISender through full DI-wired Mediator pipeline with in-memory repositories — arguably more thorough integration.
- **Entity configuration:** Sonnet correctly separated into per-entity `IEntityTypeConfiguration<T>` classes. Opus inlined in DbContext — a criterion Sonnet uniquely passes.

**What it missed (same 55/58 as Opus, different items):**
1. **Primitive obsession (L1-18)** — `int StockQuantity` and `AddStock(int)` instead of typed VO
2. **No ParallelAsync (L2-11)** — sequential product fetches (same as all 6 models tested)
3. **No migration (L3-14)** — using `EnsureCreated()` from template

**Notable strengths beyond scoring:**
- **9 friction points** — identifies the exact TRLS004 analyzer issue that forced the imperative `TryGetValue`/`TryGetError` pattern instead of Bind chains
- **Sealed aggregates** — all domain types marked `sealed` (a best practice Opus missed)
- **DTOs as sealed records** — cleaner C# 14 idiom vs Opus's mutable sealed classes
- **Batch operations** — `GetByIdsAsync()` and `SaveManyAsync()` for efficient product handling in submit/cancel
- **api.http** — comprehensive 354-line HTTP test file with environment variables, lifecycle scenarios, and error cases

**The Bind API observation:**
As the user noted, Sonnet "missed that there is Bind API which is a big miss for ROP." All handlers use the imperative `TryGetValue`/`TryGetError` pattern instead of ROP chains. The TRELLIS_FEEDBACK.md (FP-1) explains this was caused by TRLS004 analyzer not recognizing `!TryGetValue` as an `IsFailure` guard, forcing the verbose workaround. This suggests the model *attempted* ROP but fell back to imperative error handling due to analyzer friction. While this doesn't affect any specific evaluation criterion, it represents a significant style divergence from the intended Trellis ROP approach and is worth noting as a framework improvement opportunity.

**Verdict:** Claude Sonnet 4.6 Run 2 is **CAPABLE** of producing Trellis-idiomatic code. The improvement from Run 1 (26/58) to Run 2 (55/58) is directly attributable to the copilot instructions update (principle #4: built-in primitives) and demonstrates that **instruction quality is the primary lever** for lower-capability models. The remaining gap (no Bind chains) is a framework ergonomics issue, not a model capability issue.

---

## Detailed Scorecard: Claude Opus 4.6 Run 2 (Copilot)

**Date:** 2026-03-03
**Model:** Claude Opus 4.6 (via GitHub Copilot agent mode) — second attempt
**Trellis version:** 3.0.0-alpha.99
**Template version:** 1.0.5-alpha (updated with 6 copilot instruction improvements from Run 1 evaluation data)
**Build result:** 0 errors, 0 warnings
**Test result:** 146/146 (76 Domain + 12 Application + 26 Acl + 32 API)
**Output location:** `C:\github\xavier\OrderManagement-Opus46-Run2\OrderManagement`

### Level 1: Structural Consistency — 18/18

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Value objects exist | **PASS** | All 11+ present: CustomerId, OrderId, ProductId, LineItemId (RequiredGuid), FirstName, LastName, ProductName, ActorId (RequiredString), Sku (ScalarValueObject with uppercase alphanumeric 3-20), OrderStatus (RequiredEnum: Draft/Submitted/Approved/Shipped/Delivered/Cancelled), LineItemQuantity (1-999), StockQuantity (≥0), ShippingAddress (ValueObject with TryCreate). Uses EmailAddress, PhoneNumber, Money from Trellis.Primitives |
| 2 | Value objects use TryCreate | **PASS** | All VOs have `TryCreate` returning `Result<T>`. `Create` convenience methods on simpler types. `ShippingAddress.TryCreate` validates all 5 fields |
| 3 | Aggregates inherit correctly | **PASS** | `Customer : Aggregate<CustomerId>`, `Product : Aggregate<ProductId>`, `Order : Aggregate<OrderId>` |
| 4 | Line items are entities | **PASS** | `LineItem : Entity<LineItemId>` with ProductId, ProductName, LineItemQuantity, Money UnitPrice, computed LineTotal |
| 5 | State machine uses Stateless | **PASS** | `StateMachine<string, string>` with lazy init (`_machine ??= ConfigureStateMachine()`). Comment: "lazy to support EF Core materialization". Uses `FireResult()` for all transitions |
| 6 | State transitions return Result | **PASS** | All transitions (Submit, Approve, Ship, Deliver, Cancel) return `Result<Order>` via `FireResult()` wrapped in domain logic |
| 7 | Domain events defined | **PASS** | All 5 events: OrderSubmittedEvent, OrderApprovedEvent, OrderShippedEvent, OrderDeliveredEvent, OrderCancelledEvent — all implement `IDomainEvent` with `DomainEvents.Add()` |
| 8 | Specification exists | **PASS** | `OverdueOrderSpecification : Specification<Order>` with `ToExpression()`. Cutoff date injectable for testability. Has `#pragma TRLS006` suppression for `Maybe.Value` access |
| 9 | CQRS pattern used | **PASS** | 11 commands + 3 queries with handlers via Mediator source generator |
| 10 | Authorization on commands | **PASS** | `IAuthorize` on all 14 commands/queries with `RequiredPermissions`. `IAuthorizeResource<Order>` on CancelOrderCommand with ownership check |
| 11 | Permissions as constants | **PASS** | `Permissions` static class with 11 `const string` fields (customers:create, products:create, products:manage-stock, orders:create/submit/approve/ship/deliver/cancel/read/read-all) + `All` HashSet |
| 12 | Repository interfaces in Application | **PASS** | `ICustomerRepository`, `IProductRepository`, `IOrderRepository` in `Application/src/Abstractions/` |
| 13 | EF Core in Acl (T) | **PASS** | `OrderManagementDbContext` and 3 sealed repository implementations in `Acl/src/Repositories/` |
| 14 | ApplyTrellisConventions used (T) | **PASS** | `ApplyTrellisConventions(typeof(Order).Assembly)` in `ConfigureConventions`. Zero `HasConversion()` anywhere |
| 15 | Project structure matches template (T) | **PASS** | 4-project structure preserved with correct src/tests separation. Sample code retained but not harmful |
| 16 | No exceptions for control flow | **PASS** | Zero try/catch in Domain or Application layers. Only `ErrorHandlingMiddleware` in Api (template code) |
| 17 | build/test.props shared (T) | **PASS** | `build/test.props` exists with shared test configuration. Zero `GlobalUsings.cs` in test projects |
| 18 | No primitive obsession | **PASS** | All domain public APIs use typed VOs. `StockQuantity` is a proper value object (≥0 constraint). `LineItemQuantity` is typed (1-999). No raw Guid, string, int in domain method signatures |

**Perfect structural score.** Second consecutive 18/18 for Opus 4.6. Sample code (WeatherForecast, User, ZipCode, etc.) was not removed but does not break the structural criteria.

### Level 2: Behavioral Consistency — 12/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Submit validates stock | **PASS** | `Submit(List<Product> products)` — iterates line items, finds matching product, calls `product.ReserveStock(lineItem.Quantity)` per item, fails first insufficient stock |
| 2 | Cancel releases stock | **PASS** | `Cancel(List<Product>? products)` — releases stock per line item when products provided (Submitted/Approved). Draft cancel passes `null` to skip release. Test verifies stock quantity restored |
| 3 | Line item price snapshot | **PASS** | `LineItem` constructor captures `UnitPrice` (Money) from product at creation time. `LineTotal` computed from snapshot |
| 4 | Duplicate product in order | **PASS** | `TryCreate` uses `Ensure(no duplicate ProductIds)`. `AddLineItem` checks `_lineItems.Any(li => li.ProductId == lineItem.ProductId)` — returns `Error.Validation` |
| 5 | Last line item protection | **PASS** | `RemoveLineItem` returns `Error.Validation("Cannot remove the last line item from an order.")` when `_lineItems.Count` would reach 0 |
| 6 | Error types match | **PASS** | Correct taxonomy: `Error.Validation()` for business rules, `Error.NotFound()` for missing entities, `Error.Conflict()` for duplicate email/SKU, `Error.Forbidden()` for ownership violations |
| 7 | Order total computed | **PASS** | `CalculateTotal()` returns `Money` — aggregates `LineItem.LineTotal` (Quantity × UnitPrice). Used in `OrderSubmittedEvent` and response DTO |
| 8 | Overdue spec correct | **PASS** | `OverdueOrderSpecification` checks `Status == Submitted` and `_submittedAt <= cutoffDate`. Repository uses `EF.Property<DateTime?>(o, "_submittedAt")` for EF-translatable query |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 ID types extend `RequiredGuid<T>`. `RequiredGuid` auto-generates V7 GUIDs via framework base class |
| 10 | Maybe for optional phone | **PASS** | `Maybe<PhoneNumber> Phone` on Customer with `_phone` nullable backing field. `MaybeProperty()` in `CustomerConfiguration` EF config. Tests verify both `HasValue == false` and `HasValue == true` |
| 11 | ParallelAsync for draft order | **FAIL** | `CreateDraftOrderHandler` fetches customer and products sequentially — no `ParallelAsync`, `Task.WhenAll`, or concurrent execution. 0 ParallelAsync usages in entire codebase |
| 12 | Cancel resource auth check | **PASS** | `IAuthorizeResource<Order>` on CancelOrderCommand. `CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>`. Authorization checks actor == owner OR `HasPermission(OrdersReadAll)`. Explicit `IResourceLoader` registration in DI to work around cross-assembly scanning |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync(ct)` in all 3 repositories — consistent `Result<Unit>` return. `FirstOrDefaultResultAsync` for queries |

**Near-perfect behavioral implementation.** `ParallelAsync` remains the only consistent miss — now 0/7 across all runs of all models.

### Level 3: Architecture & API Consistency — 13/14

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | 4 projects with correct dependency direction: Domain → (none), Application → Domain, Acl → Application+Domain, Api → all |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages (DomainDrivenDesign, Primitives, Results, Stateless, Authorization) in Domain.csproj |
| 3 | Pipeline behaviors registered | **PASS** | `services.AddMediator(...).AddTrellisBehaviors()` — validation, authorization, resource authorization all wired |
| 4 | IActorProvider registered | **PASS** | `services.AddSingleton<IActorProvider, TestActorProvider>()` — reads X-Test-Actor header with JSON `{id, permissions}` deserialization, default Admin actor with all permissions |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs. `AddDomain()` absent (no domain services to register) |
| 6 | Endpoint paths match | **PASS** | 16 endpoints in 2026-11-12 controllers (14 required + 2 stubs for Location headers): POST/GET customers, GET customers/{id}/orders, POST orders (201), POST orders/{id}/line-items, DELETE orders/{id}/line-items/{lineItemId}, POST orders/{id}/submission/approval/shipment/delivery/cancellation, GET orders/{id}, GET orders/overdue, POST products (201), POST products/{id}/stock-additions |
| 7 | API versioning configured | **PASS** | `[ApiVersion("2026-11-12")]` on all 3 new controllers |
| 8 | SLI on every controller | **PASS** | `[ServiceLevelIndicator]` on CustomersController, OrdersController, ProductsController |
| 9 | Problem Details for errors | **PASS** | `ErrorHandlingMiddleware` + `AddProblemDetails()` preserved from template. `ToActionResult` maps Error types to proper HTTP status codes |
| 10 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync(this, nameof(GetCustomer/GetOrder/GetProduct), r => new { id = r.Id })` on all 3 creation endpoints. Stub GET endpoints provide Location header targets |
| 11 | Health check endpoint | **PASS** | `app.MapHealthChecks("/health")` in Program.cs |
| 12 | DTOs in Api layer | **PASS** | Request/Response sealed records in `Api/src/Contracts/` (CustomerContracts, OrderContracts, ProductContracts, Mappings). No domain types exposed. No DTOs in Domain/Application/Acl |
| 13 | EF Core entity configurations | **PASS** | 4 `IEntityTypeConfiguration<T>` classes in `Acl/src/Configurations/`: CustomerConfiguration (MaybeProperty for Phone, OwnsOne for ShippingAddress, unique Email index), OrderConfiguration (MaybeProperty for SubmittedAt/ShippedAt, composite index on Status+_submittedAt, AutoInclude for LineItems), ProductConfiguration (unique Sku index), LineItemConfiguration. **Improvement over Run 1** which used inline DbContext configuration |
| 14 | Migration exists | **FAIL** | No migrations — using `EnsureCreated()` in Program.cs for dev environment. Spec says EnsureCreated is acceptable but criterion requires migration |

**Excellent architecture score.** Notable improvement: Run 2 now has proper `IEntityTypeConfiguration` classes (Run 1 had inline config in DbContext). This is one of the 6 copilot instruction improvements that was added between runs.

### Level 4: Test Consistency — 8/9

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Domain tests exist | **PASS** | 76 domain tests across 10 files: OrderTests (28), ProductTests (7), CustomerTests (2), LineItemQuantityTests (2), OrderStatusTests (2), ShippingAddressTests (2), SkuTests (4), StockQuantityTests (3), TestData helper, ZipCodeTests (3, template) |
| 2 | Happy path tests | **PASS** | Create order, add line item, submit, approve, ship, deliver, full lifecycle, create customer with/without phone, create product, add/reserve/release stock, valid quantities/sku/address/status |
| 3 | Error path tests | **PASS** | Cannot create order without line items, duplicate products, insufficient stock, cannot approve draft, cannot ship draft, cannot deliver approved, cannot cancel delivered, cannot cancel already cancelled, cannot add/remove line items from submitted, cannot remove last line item, invalid sku/quantity/stock/address |
| 4 | State machine tests | **PASS** | OrderTests covers all 5 valid transitions + 7 invalid transitions: Draft→Submitted, Submitted→Approved, Approved→Shipped, Shipped→Delivered, Draft/Submitted→Cancelled (with stock release). Invalid: approve draft, ship draft, deliver approved, cancel delivered, cancel already cancelled, add/remove line item from submitted. Full lifecycle test |
| 5 | Specification test | **FAIL** | No `OverdueOrderSpecificationTests` file. The only overdue test is `List_overdue_orders_returns_200` API integration test which verifies the endpoint returns 200 but does not unit-test the specification logic (IsSatisfiedBy, boundary conditions). **Regression from Run 1** which had specification unit tests |
| 6 | Authorization tests | **PASS** | API integration: `Create_customer_without_permission_returns_403` (IAuthorize), `Cancel_without_permission_returns_403` (IAuthorizeResource). Both test full HTTP pipeline with X-Test-Actor header containing restricted permissions |
| 7 | Maybe assertion tests | **PASS** | `CustomerTests`: `Can_create_valid_customer_without_phone` — `customer.Phone.HasValue.Should().BeFalse()`. `Can_create_valid_customer_with_phone` — `customer.Phone.HasValue.Should().BeTrue()` |
| 8 | API integration tests | **PASS** | 32 API integration tests: full order lifecycle (draft→delivered), get by id, 404 nonexistent, add/remove line items, last item protection, empty line items, duplicate products, cancel draft, cancel delivered (422), insufficient stock, approve draft (422), list by customer, list overdue, cancel without permission (403), nonexistent customer (404). Customer tests: 201 with location, without phone, duplicate email (409), invalid email (400), empty name (400), list orders, without permission (403). Product tests: 201 with location, duplicate SKU (409), add stock, nonexistent product (404), invalid SKU (400). **Most comprehensive API tests of any model** (32 vs Sonnet 4.6's 18, Opus R1's 8) |
| 9 | Trellis.Testing used | **PASS** | `BeSuccess()`, `BeFailure()` assertions used consistently throughout all domain and application test layers |

**Near-perfect test score.** One regression: no specification unit test (Run 1 had one). However, Run 2 has the most comprehensive API integration test suite of any model (32 tests vs 8 in Run 1), compensating by verifying overdue orders at the API level.

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | `TRELLIS_FEEDBACK.md` — 119 lines, well-structured with Summary, Friction Points, What Worked Well, Suggested New Features, Copilot Instructions Feedback |
| 2 | Friction points specific | **PASS** | 7 friction points: FP-1 (TapAsync Task/ValueTask ambiguity — HIGH, blocked ROP chains), FP-2 (TRLS003 doesn't recognize early-return guard — MEDIUM), FP-3 (AddResourceAuthorization cross-assembly scanning — HIGH, caused 500 errors), FP-4 (DomainEvents protected, can't assert in tests — LOW), FP-5 (Trellis.Unit vs Mediator.Unit disambiguation — MEDIUM), FP-6 (SaveChangesResultUnitAsync naming — LOW), FP-7 (Money.Create throws instead of Result — LOW). Each has Category, Severity, Context, What Happened, Workaround Used, Suggested Improvement |
| 3 | What Worked Well present | **PASS** | 11 specific items: value object primitives (source gen), ApplyTrellisConventions, Result\<T\> ROP chains, ToActionResult/ToCreatedAtActionResultAsync, AddScalarValueValidation, FirstOrDefaultResultAsync/MaybeAsync, MaybeProperty for EF, FireResult on Stateless, Trellis analyzers (TRLS001/TRLS007), Error types with HTTP mapping, IAuthorize/IAuthorizeResource authorization pipeline |
| 4 | Copilot instructions feedback | **PASS** | 4 specific items: AddResourceAuthorization should document single-assembly limitation and suggest passing Acl assembly, Handler ROP section should give concrete TapAsync disambiguation example, SaveChangesResultUnitAsync should be mentioned in EF Core section, ToActionResult should document Error→HTTP status code mapping (Validation→422 not 400) |

**Perfect feedback score.** 3 suggested new features (multi-assembly AddResourceAuthorization, public DomainEvents reader for testing, prominent Traverse documentation). FP-1 (TapAsync ambiguity) and FP-3 (cross-assembly resource loader) are HIGH-severity items that directly affected handler implementation — the model fell back to imperative style with `#pragma TRLS003` suppressions instead of ROP chains, which is documented honestly.

### Overall Assessment

**Score: 55/58 (95%) — PASS (target: 53+/58)**

Claude Opus 4.6 Run 2 achieves the same passing score as Run 1 but with a **different failure profile**, demonstrating consistent capability with slight variations:

**Comparison: Opus 4.6 Run 1 vs Run 2 (both 55/58):**

| Criterion | Run 1 | Run 2 | Change |
|-----------|-------|-------|--------|
| EF Core entity configurations (L3-13) | **FAIL** (inline DbContext) | **PASS** (4 IEntityTypeConfiguration classes) | **Improved** |
| Specification test (L4-5) | **PASS** (OverdueOrderSpecificationTests) | **FAIL** (no spec unit test) | **Regressed** |
| ParallelAsync (L2-11) | **FAIL** | **FAIL** | Same |
| Migration exists (L3-14) | **FAIL** | **FAIL** | Same |

**Improvements in Run 2 (attributable to updated copilot instructions):**
- **IEntityTypeConfiguration**: 4 per-entity configuration classes in `Acl/src/Configurations/` (Run 1 had all config inline in DbContext.OnModelCreating). This was one of the 6 instruction improvements added between runs.
- **Test volume**: 146 tests (vs 114 in Run 1) — 32 API integration tests (vs 8) with comprehensive scenario coverage including 422 UnprocessableEntity for invalid transitions, 403 for permission/ownership, 404 for nonexistent entities.
- **Sample code accommodation**: Retained all template sample code (WeatherForecast, ZipCode, etc.) without interference — tests pass, build clean.
- **IResourceLoader workaround**: Explicit `AddScoped<IResourceLoader<CancelOrderCommand, Order>, CancelOrderResourceLoader>()` alongside `AddResourceAuthorization()` — documented in TRELLIS_FEEDBACK.md FP-3 as a cross-assembly scanning issue.

**Regression in Run 2:**
- **No specification unit test** (L4-5) — Run 1 had `OverdueOrderSpecificationTests` with IsSatisfiedBy checks. Run 2 only tests overdue at the API level (`List_overdue_orders_returns_200`). This is a minor regression since the spec exists and works correctly.

**Handler pattern observation:**
Run 2 handlers show a mixed pattern — `ApproveOrderHandler`, `ShipOrderHandler`, `DeliverOrderHandler`, `RemoveLineItemHandler`, `AddStockHandler` use proper ROP Bind/BindAsync chains. `AddLineItemHandler` and `CreateDraftOrderHandler` use imperative style with `#pragma TRLS003` suppressions. The TRELLIS_FEEDBACK.md honestly attributes this to TapAsync Task/ValueTask overload ambiguity (FP-1), which blocked clean ROP chains for handlers that need to call async save operations.

**Notable implementation details:**
- **Stateless lazy init**: `private StateMachine<string, string>? _machine; Machine => _machine ??= ConfigureStateMachine()` — same pattern as Run 1, idiomatic for EF Core materialization
- **Backing fields for Maybe\<T\>**: `_phone`, `_submittedAt`, `_shippedAt` with `MaybeProperty()` EF config — correct pattern
- **Composite index**: `builder.HasIndex("Status", "_submittedAt")` in OrderConfiguration for efficient overdue queries
- **AutoInclude for LineItems**: `builder.Navigation(o => o.LineItems).AutoInclude()` — eliminates need for explicit Include() calls
- **OwnsOne for ShippingAddress**: Customer's ShippingAddress mapped as owned entity — correct DDD pattern for value objects with multiple columns

**Verdict:** Claude Opus 4.6 Run 2 is **CAPABLE** and **consistent**. The 55/58 score matches Run 1, confirming that Opus 4.6 reliably produces Trellis-idiomatic code. The copilot instruction improvements (particularly the IEntityTypeConfiguration guidance) directly improved L3-13, while a minor test coverage gap (specification) caused an offsetting L4-5 regression. The net result is the same high-quality implementation with a different error profile — exactly what you'd expect from a capable model across independent runs.

---

## Notes

- Claude Opus 4.6 is the **first model to pass** the 53+/58 threshold, scoring 55/58 (95%).
- Claude Opus 4.6 Run 2 is the **third model to pass**, also scoring 55/58 (95%) with updated template (alpha.99). Demonstrates consistent capability across runs — same score, different failure profile (gained entity configs, lost spec test).
- Claude Sonnet 4.6 Run 2 is the **second model to pass**, also scoring 55/58 (95%) — the largest single-run improvement of any model (26→55).
- The Sonnet 4.6 Run 1 → Run 2 improvement (26→55) proves that **copilot instruction quality is the primary lever** for enabling lower-capability models. The only change between runs was the addition of principle #4 (built-in primitives list).
- Both CAPABLE models independently identified the ResourceAuthorizationBehavior singleton/scoped conflict as a HIGH-severity issue — confirming it as a real framework bug.
- No model has used `ParallelAsync` — this is now 0/7 across all runs, making it the most consistently missed criterion. An explicit example in copilot instructions is strongly warranted.
- No model has created EF Core migrations — 0/7 across all runs. Consider whether this criterion should remain or if `EnsureCreated()` is acceptable for the lab context.
- Test count is misleading — more tests does not mean better when the underlying patterns are wrong.
- Sonnet 4 produced more tests (125 vs 82) but got the architecture fundamentally wrong.
- GPT-5.2 Codex Max produced the best architecture of any non-Opus model but wrote zero tests — 13 free points left on the table.
- The copilot instructions and API reference are sufficient for capable models (Opus 4.6 proved this, Sonnet 4.6 Run 2 confirmed it after instruction improvement). Weaker models simply don't follow instructions well enough.
- Gemini 2.5 Pro shows a different failure mode: it reads instructions carefully but hallucinates API details. If it could get the namespaces/types right, it would score significantly higher.
- GPT-5.2 Codex Max shows yet another failure mode: it gets the API surface correct but ignores "soft" requirements (tests, feedback file). Its architectural score (40/45 on L1-L3) is the highest of any non-Opus model.
- Opus 4.6's feedback identified 2 HIGH-severity real framework issues (Stateless/EF Core lazy init, ResourceAuthorizationBehavior singleton/scoped conflict) that should inform Trellis development.
- Opus 4.6 Run 2's feedback identified the same cross-assembly ResourceAuthorization issue (FP-3) and TapAsync overload ambiguity (FP-1) as HIGH-severity — both friction points are consistent across runs. The TapAsync ambiguity forced some handlers to use imperative `#pragma TRLS003` patterns instead of clean ROP chains.
- Sonnet 4.6 Run 2's feedback adds FP-1 (TRLS004 guard pattern) as a new HIGH-severity issue — the analyzer's inability to recognize `!TryGetValue` as an `IsFailure` guard forces verbose imperative error handling instead of clean Bind chains.
- Future runs should scaffold fresh from template and use the exact Step 4 prompt from the training lab.
- The alpha.99 template with 6 copilot instruction improvements (Handler ROP, Parallel Async, State Machines, IEntityTypeConfiguration, Migrations, Task/ValueTask) successfully improved L3-13 (entity configs) for Opus Run 2. The remaining gaps (ParallelAsync, Migration, spec test) are not instruction-addressable — they may require framework examples or stricter spec language.
- **Alpha.104 criteria change:** Removed SLI per-controller (now global) and replaced Migration with EnsureCreated. L3 went from 14→13, total from 58→57, goal from 53→52. All Opus 4.6 runs should be compared on percentage basis: Run 1: 95%, Run 2: 95%, Run 4: 93%.
- **ParallelAsync breakthrough:** Run 4 is the first run (out of 8 total) to use `Result.ParallelAsync` correctly. The alpha.104 copilot instructions with explicit fluent chain guidance (`Result.ParallelAsync(() => ..., () => ...).WhenAllAsync(...)`) finally closed this gap. L2-11 went from 0/7 → 1/1.
- **L4 is the remaining weakness for Opus 4.6:** Runs 1-2 scored 8-9/9, Run 4 scored 5/9. The missing spec unit test is a consistent miss (3/4 runs). Auth tests and Maybe assertions are new misses that could be addressed with copilot instruction examples. The test isolation bug (hardcoded email) is a genuine implementation error.
- **Friction point patterns across Opus 4.6 runs:** CA1725 `ct` naming (Runs 2, 4), DomainEvents protected (Run 4), HasIndex with Maybe\<T\> (Run 4). The `ct` naming issue persists because copilot instruction examples still use `ct`. Consider updating all handler examples to `cancellationToken`.

---

## Detailed Scorecard: Claude Opus 4.6 Run 4 (Copilot)

**Date:** 2026-03-10
**Model:** Claude Opus 4.6 (via GitHub Copilot agent mode)
**Trellis version:** 3.0.0-alpha.104
**Template version:** 1.0.3-alpha
**Build result:** 0 errors, 0 warnings
**Test result:** 74/75 (1 failure — `Full_order_lifecycle_happy_path` test isolation bug: hardcoded email)
**Test breakdown:** 50 Domain + 0 Application + 12 API integration + 13 template = 75
**Output location:** `C:\temp\OrderManagement-Opus46-Run4\OrderManagement`

> **First run on alpha.104 criteria.** This run uses updated criteria: L3 has 13 items (removed SLI per-controller, replaced Migration with EnsureCreated). Total L1-L5 = 57 (was 58).

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
| 7 | Order total computed | **PASS** | `CalculateTotal()` uses Money.Multiply + Money.Add; test verifies 3 × $9.99 = $29.97 |
| 8 | Overdue spec correct | **PASS** | Submitted status + 7-day threshold, parameterized DateTime, used with `.Where(spec)` on IQueryable |
| 9 | IDs use RequiredGuid with V7 | **PASS** | All 4 IDs use RequiredGuid with NewUniqueV7() |
| 10 | Maybe for optional phone | **PASS** | `partial Maybe<PhoneNumber>` on Customer — source generator handles persistence |
| 11 | ParallelAsync for draft order | **PASS** | `Result.ParallelAsync` + `WhenAllAsync` in CreateDraftOrderHandler |
| 12 | Cancel resource auth check | **PASS** | CancelOrderCommand: IAuthorizeResource\<Order\> with ownership check (actor == CreatedByActorId OR has orders:read-all) |
| 13 | SaveChangesResultAsync used | **PASS** | `SaveChangesResultUnitAsync` in all 3 repositories |

**First run to achieve 13/13 on L2.** ParallelAsync finally used correctly — this had been 0/7 across all prior runs.

### Level 3: Architecture & API Consistency — 13/13

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Clean architecture layers | **PASS** | Domain → Application → Acl → Api; correct dependency direction |
| 2 | Domain has no external deps | **PASS** | Only Trellis packages + Stateless |
| 3 | Pipeline behaviors registered | **PASS** | `AddMediator` + `AddTrellisBehaviors()` + `AddResourceAuthorization(assembly)` |
| 4 | IActorProvider registered | **PASS** | TestActorProvider reads X-Test-Actor header with JSON deserialization |
| 5 | DI extension per layer | **PASS** | `AddApplication()`, `AddAntiCorruptionLayer()`, `AddPresentation()` wired in Program.cs |
| 6 | Endpoint paths match | **PASS** | All endpoints present: 3 Customer, 3 Product, 10 Order (16 total including reads) |
| 7 | API versioning configured | **PASS** | `VersionByNamespaceConvention` + controllers in `v2026_11_12/Controllers/` |
| 8 | Problem Details for errors | **PASS** | `AddProblemDetails()` + ErrorHandlingMiddleware with ProblemDetailsService |
| 9 | 201 for creation with Location | **PASS** | `ToCreatedAtActionResultAsync` on POST Customers, Products, Orders |
| 10 | Health check endpoint | **PASS** | `MapHealthChecks("/health")` — integration test confirms 200 OK |
| 11 | DTOs in Api layer | **PASS** | Models in `v2026_11_12/Models/` (CustomerModels.cs, OrderModels.cs, ProductModels.cs) |
| 12 | EF Core entity configurations | **PASS** | 4 IEntityTypeConfiguration classes in `Acl/src/Configurations/` |
| 13 | EnsureCreated on startup | **PASS** | `dbContext.Database.EnsureCreated()` in Development mode; no migrations |

**First run to achieve 13/13 on L3.** SLI per-controller criterion was removed (now global registration only).

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

**L4 dropped from 8-9/9 (prior runs) to 5/9.** Notable regressions: spec test (consistent miss across runs), auth tests (new miss), Maybe assertions (new miss), API integration test failure (new).

### Level 5: Feedback Quality — 4/4

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Feedback file exists | **PASS** | TRELLIS_FEEDBACK.md in repo root |
| 2 | Friction points specific | **PASS** | 4 FPs with category, severity, context, workaround, suggestion: CA1725 `ct` naming (Medium), Money.Create throws (Low), DomainEvents protected (Low), HasIndex with Maybe\<T\> (Medium) |
| 3 | What Worked Well present | **PASS** | 10 specific items: ApplyTrellisConventions, Money auto-mapping, Specification composability, ToActionResult, AddScalarValueValidation, ParallelAsync, FireResult, SaveChangesResultUnitAsync, FirstOrDefaultResultAsync, Trellis Analyzers |
| 4 | Copilot instructions feedback | **PASS** | Identifies `ct` → `cancellationToken` naming issue and Maybe\<T\> HasIndex documentation gap |

### Overall Assessment

**Score: 53/57 (93.0%) — PASS (target: 52+/57)**

**What improved over prior runs:**
- **ParallelAsync** ✅ — First ever pass on L2-11 (0/7 → 1/1). The alpha.104 copilot instructions with explicit `ParallelAsync` fluent chain guidance worked.
- **SaveChangesResultUnitAsync** ✅ — Correctly named (prior instruction updates).
- **partial Maybe\<T\>** ✅ — Used alpha.104 source generator pattern (no MaybeProperty() calls).
- **[StringLength]** ✅ — All RequiredString subclasses have [StringLength] attributes.
- **Namespace-based versioning** ✅ — `VersionByNamespaceConvention` + versioned folders.
- **EnsureCreated** ✅ — Now matches criteria (was always passing but criteria previously required migrations).
- **All L2 criteria** — First 13/13 on behavioral consistency.

**What regressed:**
- **L4 dropped to 5/9** — Lost 3-4 points from prior runs:
  - Spec test: Consistent miss (3 of 4 Opus runs miss this)
  - Authorization tests: No Application-layer handler tests, no 403 integration test
  - Maybe assertions: Uses `HasValue.Should().BeTrue()` instead of `Should().HaveValue()`
  - API test failure: `Full_order_lifecycle_happy_path` uses hardcoded `john@example.com` → fails on non-clean DB

**Notable implementation details:**
- **Lazy StateMachine with string keys**: `StateMachine<string, string>` using `OrderStatus.Name` as states — same pattern as Runs 1-2
- **Submit/Cancel stock delegates**: `Submit(Func<ProductId, int, Result<Unit>>)` and `Cancel(Action<ProductId, int>?)` — clever separation of concern, domain defines the contract, handler provides the implementation
- **GetAwaiter().GetResult()**: SubmitOrderHandler and CancelOrderHandler use blocking sync-over-async inside the stock reservation/release delegates. This works but is an anti-pattern — the domain methods accept sync delegates, forcing the async handler to block
- **CA1725 compliance**: All handlers use `cancellationToken` parameter name (not `ct`) — addressed FP-1 proactively
- **Composite index**: `builder.HasIndex("Status", "_submittedAt")` — correctly uses string-based backing field reference for Maybe\<T\> property
- **AutoInclude**: `builder.Navigation(o => o.LineItems).AutoInclude()` — eliminates explicit Include() calls
- **OwnsOne ShippingAddress**: Mapped with `HasColumnName("ShippingStreet")` etc. — correct owned entity pattern
- **Comprehensive .http file**: 20+ request scenarios including error paths — thorough API documentation

**Friction points from TRELLIS_FEEDBACK.md:**
1. **FP-1: CA1725 `ct` naming** (Medium) — Copilot instructions use `ct` but TreatWarningsAsErrors enforces `cancellationToken`. *Action: Update instruction examples to use `cancellationToken`.*
2. **FP-2: Money.Create throws** (Low) — Inconsistent with errors-as-values principle. *Already documented in prior runs.*
3. **FP-3: DomainEvents protected** (Low) — Can't verify specific events in unit tests. *Action: Consider public read-only accessor or Trellis.Testing extension.*
4. **FP-4: HasIndex with Maybe\<T\>** (Medium) — String-based backing field reference needed. *Action: Document in Maybe\<T\> section of copilot instructions.*

**Verdict:** Claude Opus 4.6 Run 4 is **CAPABLE** with continued consistency. The 53/57 (93%) score on the new alpha.104 criteria confirms that Opus 4.6 reliably produces Trellis-idiomatic code across framework versions. The major improvement is **L2 perfection** (first-ever 13/13) driven by ParallelAsync finally being used — directly attributable to the updated copilot instructions. The L4 regression to 5/9 is the main weakness, primarily from missing authorization/specification tests and a test isolation bug. The implementation quality remains excellent — clean architecture, correct patterns, comprehensive domain tests, and high-quality feedback.
