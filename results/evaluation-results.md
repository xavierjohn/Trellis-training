# Trellis AI Evaluation Results

Tracks how well different AI models implement the Order Management spec using Trellis conventions.

**Evaluation spec:** Order Management (see [`specs/order-management.md`](../specs/order-management.md))
**Scoring framework:** 57 criteria across 5 levels (see [`docs/training-lab.md`](../docs/training-lab.md))
**Goal:** Total score of 52+/57

---

## Current cohort — Trellis `3.0.0-alpha.360` (2026-06-08, rescored 2026-06-08 after L2.11 rubric fix)

| Date | AI Model | Build | Tests | L1 (/18) | L2 (/13) | L3 (/13) | L4 (/9) | L5 (/4) | Total (/57) | Verdict |
|------|----------|-------|-------|----------|----------|----------|---------|---------|-------------|---------|
| 2026-06-08 | Claude Opus 4.8 (CLI) | 0 errors | 95/95 | 18/18 | 13/13 | 13/13 | 8/9 | 4/4 | **56/57** | **PASS** |
| 2026-06-08 | Claude Sonnet 4.6 (CLI) | 0 errors | 73/73 | 17/18 | 13/13 | 13/13 | 9/9 | 4/4 | **56/57** | **PASS** |
| 2026-06-08 | Claude Opus 4.7 1M (CLI) | 0 errors | 45/45 | 18/18 | 13/13 | 13/13 | 7/9 | 4/4 | **55/57** | **PASS** |
| 2026-06-08 | GPT-5.5 (CLI) | 0 errors | 23/23 | 18/18 | 13/13 | 12/13 | 7/9 | 4/4 | **54/57** | **PASS** |
| 2026-06-08 | Claude Haiku 4.5 (CLI) | 0 errors | 37/37 | 15/18 | 11/13 | 8/13 | 7/9 | 2/4 | **43/57** | **FAIL** |

> **Rubric fix (2026-06-08):** L2.11 was rewritten from *"`CreateDraftOrder` fetches customer and products in parallel (`ParallelAsync`)"* to *"batched product load (`FindManyByIdAsync`) on a single scoped `DbContext`, NOT N+1 and NOT parallel-on-shared-DbContext"*. The old wording contradicted cookbook Recipe 21 (parallelizing two repos that share a scoped `DbContext` races EF Core's change tracker). All five alpha.360 models had used the framework-correct batched-load pattern; the previous scoring penalized them for it. Four of the five gain +1; Haiku also gains +1 because its persistence tests confirm it batched too. L2.13 was simultaneously rewritten from *"repositories use `SaveChangesResultAsync`"* to *"under `AddTrellisUnitOfWork<TContext>` handlers/repositories MUST NOT call `SaveChangesAsync`/`SaveChangesResultAsync` at all"*; this was a documentation fix only — no model's score changed because all five had already been counted as passing under the UoW interpretation.

> **Historical scores are NOT retroactively rescored.** The alpha.104/106 entries below were scored against the old rubric wording and remain at their original totals for historical comparison only.

---

## Historical results — alpha.104 / alpha.106 era (kept for diff reference)

| Date | AI Model | Build | Tests | L1 (/18) | L2 (/13) | L3 (/13) | L4 (/9) | L5 (/4) | Total (/57) | Verdict |
|------|----------|-------|-------|----------|----------|----------|---------|---------|-------------|---------|
| 2025-07-10 | Claude Opus 4.6 (Copilot) | 0 errors | 74/75 | 18/18 | 13/13 | 13/13 | 5/9 | 4/4 | **53/57** | **PASS** |
| 2026-03-10 | GPT-5.4 (Copilot) | 0 errors | 3/3 | 17/18 | 11/13 | 12/13 | 1/9 | 4/4 | **45/57** | **FAIL** |
| 2026-03-10 | Claude Sonnet 4.6 (Copilot) | 0 errors | 127/127 | 18/18 | 13/13 | 13/13 | 7/9 | 4/4 | **55/57** | **PASS** |

> The alpha.104/106 entries pre-date the v4 typed-accessor pattern (`IIdentifyResource` + `IAuthorizedResource`), the `Trellis.Mediator.FluentValidation` package split, the renamed permission set on `Error.Conflict` (now requires `ReasonCode`), and several other API shifts. Direct comparison to the alpha.360 cohort above isn't apples-to-apples because the framework surface changed underneath. The progression measured by these rows is **alpha.106 → alpha.360 → models held up the same or better against a moving target**.

---

## Cross-cohort observations

**Top alpha.360 models (Opus 4.8, Sonnet 4.6) tied at 56/57; Opus 4.7 close behind at 55/57.** With the L2.11 criterion now correctly framed as "batched intra-DbContext load", the only remaining sub-criterion that keeps the top two models off a perfect 57/57 is per-model: Opus 4.8 missed L4.7 (Maybe assertion extensions); Sonnet 4.6 missed L1.18 (primitive `int`/`string` parameters in `LineItem` ctor). No structural ceiling — both gaps are realistic fixes a small Copilot-instructions tweak could close.

**GPT-5.5 closed most of the historical gap vs GPT-5.4 (45 → 54, +9).** Largest gain area: L4 went from 1/9 (catastrophic) to 7/9 (only the two assertion-style misses remain). The Trellis surface improvements (clearer cookbook recipes, v4 typed accessor, stronger analyzer messages) appear to lift the gpt-5 family the most in absolute terms.

**Haiku 4.5 is the only FAIL.** It produced clean code and 37 passing tests, but materially diverged from the spec on three load-bearing surfaces (permissions list, customer-name shape, API versioning). This is exactly the "small model under-budget on spec adherence" failure mode the lab is designed to detect — Haiku's run report self-assessed at L1–L5 maturity-tier "Met" rather than scoring against the 18 sub-criteria in L1 alone. A future iteration should consider stricter prompting for small models that warns explicitly about following the spec's exact permission strings and endpoint paths.

**Best-in-class behaviors worth lifting into the Copilot instructions:**
- **Sonnet 4.6** was the only model to use **both** `Trellis.Testing` assertion extensions (`.Should().BeSuccess()` + `.Should().HaveValue()`) systematically.
- **Opus 4.8** was the only model to **explicitly flag the L2.11/Recipe 21 contradiction** rather than either ignoring it or chasing the point and introducing an EF race. That feedback was acted on — the rubric was fixed in this same PR.
- **GPT-5.5** produced the most compact code (76 source files vs Opus 4.8's 76 vs my 70 vs Sonnet 4.6's 90), without sacrificing structural rubric points. Worth studying how it bundles related VOs into shared files without losing type distinctness.

---

## Detailed scorecards — alpha.360 cohort

### Claude Opus 4.8 — 56/57 PASS

**Date:** 2026-06-08 · **Trellis:** 3.0.0-alpha.360 · **Run mode:** GitHub Copilot CLI background sub-agent (`model=claude-opus-4.8`)
**Build:** 0 warnings, 0 errors · **Tests:** 95/95 passing (Domain 52 · Application 25 · ACL 6 · Api 12)
**Wall clock:** ~89 min · **Files:** 56 src + 20 test = 76 .cs files

| Level | Score | Highlights |
|---|---|---|
| L1 Structural | **18/18** | All required VOs (CustomerId/OrderId/ProductId/LineItemId/Sku/Quantity/StockQuantity/UnitPrice/FirstName/LastName/ProductName/ShippingAddress); `Customer.PhoneNumber` is `Maybe<PhoneNumber>`; `LineItem : Entity<LineItemId>`; `LazyStateMachine<OrderStatus, OrderTrigger>` + `FireResult` returning `Result`; all 5 events; `OverdueOrderSpecification`; `CancelOrderCommand` implements `IAuthorize` + `IAuthorizeResource<Order>` + `IIdentifyResource<Order, OrderId>` AND handler injects `IAuthorizedResource<CancelOrderCommand, Order>` (cookbook Recipe 31); 11 spec permissions exactly; `ApplyTrellisConventions`; 0 try/catch. |
| L2 Behavioral | **13/13** | Two-pass stock validation on Submit, release on Cancel from Submitted/Approved only, line-item price snapshot, duplicate-product rejected, last-line-item protected, error taxonomy correct, order total, OverdueSpec verified against SQLite, RequiredGuid V7, `Maybe<PhoneNumber>`, ownership-checked Cancel, **batched product load** via `FindManyByIdAsync` (new L2.11), no handler-side `SaveChanges*` calls (new L2.13). The only model to flag the original L2.11/Recipe 21 contradiction in its TRELLIS_FEEDBACK before the rubric was fixed. |
| L3 Architecture | **13/13** | 4-project Clean Arch; Domain references only Trellis + runtime; Mediator pipeline behaviors; `DevelopmentActorProvider` reads `X-Test-Actor`; per-layer DI; **14 endpoints exact** with spec's `/submission` `/approval` `/shipment` `/delivery` `/cancellation` naming; namespace API versioning `v2026_11_12`; RFC 9457 ProblemDetails; 201+Location with `api-version` round-tripped; `/health` version-neutral; DTOs in `Api/src/2026-11-12/Models/`; 3 IEntityTypeConfigurations; EnsureCreatedAsync. |
| L4 Tests | **8/9** | 95 tests across 8 test files including state-machine valid + invalid transitions, owner/non-owner/admin cancel, missing-permission 403, full Draft→Delivered HTTP round-trip, real SQLite OwnsMany round-trip + `Maybe<DateTime>` translation tests. **64 hits of `.Should().BeSuccess()` / `.Should().BeFailure()`** — used `Trellis.Testing` assertions properly. **Miss: L4.7** — Maybe assertions used `.HasValue.Should().BeTrue()` not `.Should().HaveValue()` / `.Should().BeNone()`. |
| L5 Feedback | **4/4** | TRELLIS_FEEDBACK.md with severity-ranked frictions: (1) `RequiredInt<T> + [NonNegative]` still rejects 0 — caught at runtime only; (2) `Trellis.Mediator.FluentValidation` package split silent-no-validation if missed; (3) `Maybe<T>` EF query translation requires `AddTrellisInterceptors()`. "What worked well" present; rubric-gap L2.11 escalated (and acted upon — see rubric-fix note above). |

---

### Claude Sonnet 4.6 — 56/57 PASS

**Date:** 2026-06-08 · **Trellis:** 3.0.0-alpha.360 · **Run mode:** GitHub Copilot CLI background sub-agent (`model=claude-sonnet-4.6`)
**Build:** 0 warnings, 0 errors · **Tests:** 73/73 passing (Domain 11 · Application 10 · ACL 11 · Api 12 + helpers)
**Wall clock:** ~55 min · **Files:** 75 src + 44 test = 119 .cs files (the densest layout in the cohort)

| Level | Score | Highlights |
|---|---|---|
| L1 Structural | **17/18** | All 11 spec permissions **exactly** by name; `CustomerFirstName` / `CustomerLastName` as separate VOs (slight rename); `Email` (own VO not Trellis primitive); `SKU`, `ProductName`, `UnitPrice` as VOs; v4 typed accessor injected on Cancel; 0 try/catch; preserved template structure. **Miss: L1.18** — `LineItem` ctor takes raw `string productName, int quantity, decimal unitPrice` (primitive obsession in a domain method); `Product.StockQuantity` is `int`. |
| L2 Behavioral | **13/13** | Stock validation, cancel release, price snapshot, last-line-item protection, error taxonomy, order total, OverdueSpec, RequiredGuid V7, `Maybe<string>` for PhoneNumber (`Maybe<>` shape is present even if inner type is primitive), ownership-checked Cancel, batched product load (new L2.11), no handler-side `SaveChanges*` (new L2.13). |
| L3 Architecture | **13/13** | 4-project Clean Arch; per-layer DI; **14 endpoints exact**; versioned controllers under `2026-11-12/Controllers/`; DTOs in `2026-11-12/Models/`; ProblemDetails; `/health`; EnsureCreated; 3 EntityTypeConfigurations. |
| L4 Tests | **9/9** | Only model to use **both** `Trellis.Testing` assertion extensions: ~38 `.Should().BeSuccess()` / `.BeFailure()` hits across 6 test files AND 4 `.Should().HaveValue()` / `.BeNone()` hits across 2 test files. Domain rules, state machine, specification, authorization (owner/admin/stranger), API integration covered. |
| L5 Feedback | **4/4** | 13.2 KB TRELLIS_FEEDBACK.md, 3 categorized top frictions: (1) `Trellis.Mediator.FluentValidation` not in scaffold; (2) template version pin alpha.337 lags; (3) `IAuthorizedResource<,>` accessor NOT registered by the assembly-scan `AddResourceAuthorization` overload (production-bug-class friction). What-worked-well present. |

---

### Claude Opus 4.7 (1M context) — 55/57 PASS

**Date:** 2026-06-08 · **Trellis:** 3.0.0-alpha.360 · **Run mode:** GitHub Copilot CLI main-session run (`model=claude-opus-4.7-1m-internal`) — this is the reference run that lives in [`../after/OrderManagement/`](../after/OrderManagement/)
**Build:** 0 warnings, 0 errors · **Tests:** 45/45 passing (Domain 31 · Application 6 · ACL 3 · Api 5)
**Wall clock:** ~1h 45m · **Files:** 70 src + 12 test = 82 .cs files

| Level | Score | Highlights |
|---|---|---|
| L1 Structural | **18/18** | All 19 VOs (one VO per file pattern); separate `FirstName`/`LastName`/`Sku`/`UnitPrice`/`LineItemQuantity`/`StockQuantity`; composite `ShippingAddress` as plain `ValueObject` (not `[OwnedEntity]` since Domain mustn't reference EF); 5 events; OverdueSpec; CancelOrder full v4 pattern + handler accessor; 11 permissions exact; 0 try/catch. |
| L2 Behavioral | **13/13** | Atomic stock reservation via two-phase preflight + `.Bind` (to satisfy TRLS010); cancel releases for Submitted/Approved; price snapshot; duplicate-product rejected; last-line-item protected; error taxonomy; order total; OverdueSpec; RequiredGuid V7; `Maybe<PhoneNumber>`; ownership-checked Cancel; batched `FindManyByIdAsync` load in CreateDraftOrder (new L2.11); no handler-side `SaveChanges*` (new L2.13). |
| L3 Architecture | **13/13** | 4-project Clean Arch; `DevelopmentActorProvider` with admin defaults per spec §5.5; 14 endpoints + 2 hidden helper routes for `CreatedAtRoute` Location; namespace-versioned controllers; DTOs in versioned Models folder; ProblemDetails + UseExceptionHandler + UseStatusCodePages; `/health`; 3 IEntityTypeConfigurations; EnsureCreatedAsync. |
| L4 Tests | **7/9** | 45 tests covering state machine, value object validation theory, OverdueSpec, direct `IAuthorizeResource.Authorize` owner/admin/stranger tests, ACL round-trip + unique constraint, end-to-end `OrderLifecycleTests` (lifecycle + 403/404/409 + /health). **Misses: L4.7 + L4.9** — used `.IsSuccess.Should().BeTrue()` / `.HasValue.Should().BeTrue()` instead of the `.Should().BeSuccess()` / `.Should().HaveValue()` extensions from `Trellis.Testing`. |
| L5 Feedback | **4/4** | 13.8 KB TRELLIS_FEEDBACK.md with 5 categorized sections: AspTemplate two alphas behind framework; TRLS010 vs Tap+Result analyzer (requires `.Bind` refactor); `DevelopmentActorProvider` empty default permissions footgun; 5 minor frictions; what-worked-well. |

---

### GPT-5.5 — 54/57 PASS

**Date:** 2026-06-08 · **Trellis:** 3.0.0-alpha.360 · **Run mode:** GitHub Copilot CLI background sub-agent (`model=gpt-5.5`)
**Build:** 0 warnings, 0 errors · **Tests:** 23/23 passing (Domain 7 · Application 7 · ACL 2 · Api 7)
**Wall clock:** ~43 min — **fastest in the cohort by 12+ minutes**
**Files:** 44 src + 4 test = 48 .cs files — densest VO packing (3 shared VO files instead of one-per-VO)

| Level | Score | Highlights |
|---|---|---|
| L1 Structural | **18/18** | All required VO types present (uses Trellis `Money` for unit price, Trellis `EmailAddress` + `PhoneNumber`); `Quantity` and `StockAdjustmentQuantity` as distinct VOs; v4 typed accessor implemented cleanly with `IAuthorizedResource<CancelOrderCommand, Order>` injected; 11 permissions exact; ApplyTrellisConventions; 0 try/catch. VOs bundled into `CustomerValueObjects.cs` / `ProductValueObjects.cs` / `IdentityValueObjects.cs` — file organization differs but types are distinct so rubric passes. |
| L2 Behavioral | **13/13** | Stock validation/release, line-item price snapshot, duplicate-product rejected, last-line-item protected, error taxonomy, order total, OverdueSpec (overdue query runs in-memory due to SQLite `DateTimeOffset` limitation — self-flagged), RequiredGuid V7, `Maybe<PhoneNumber>`, ownership-checked Cancel, batched `GetByIdsAsync` (new L2.11), no handler-side `SaveChanges*` (new L2.13). |
| L3 Architecture | **12/13** | 4-project Clean Arch; per-layer DI; **14 endpoints exact** (no helper-route additions); 2026-11-12 namespace-versioned controllers under `Api/src/2026-11-12/`; ProblemDetails; `Created()` Location URL; /health; 4 IEntityTypeConfigurations (Customer/Order/Product/LineItem); EnsureCreatedAsync. **Miss: L3.11** — DTOs in single `Api/src/Models.cs` rather than `Api/src/{version}/Models/` folder (self-flagged). |
| L4 Tests | **7/9** | 23 tests across 4 layer-bundled test files. 26+ authorization-related test hits (owner/admin/forbidden). **Misses: L4.7 + L4.9** — same as Opus 4.7: `IsSuccess.Should().BeTrue()` / `HasValue.Should().BeTrue()` instead of Trellis.Testing extensions. |
| L5 Feedback | **4/4** | 4.7 KB TRELLIS_FEEDBACK.md, 3 categorized frictions with severity + workaround + suggestion (version drift; spec-required HTTP 400 vs Trellis.Asp default 422; SQLite `DateTimeOffset` query limitations). What-worked-well present (4 specific positives). |

**vs historical GPT-5.4 (alpha.106, 45/57):** +9 points. Largest gain in L4 (1/9 → 7/9). Frame change from "barely passes build" to "structurally correct, comparable to Sonnet/Opus on most criteria".

---

### Claude Haiku 4.5 — 43/57 FAIL

**Date:** 2026-06-08 · **Trellis:** 3.0.0-alpha.360 · **Run mode:** GitHub Copilot CLI background sub-agent (`model=claude-haiku-4.5`)
**Build:** 0 warnings, 0 errors · **Tests:** 37/37 passing (Domain 13 · Application 15 · ACL 11 · Api 15)
**Wall clock:** ~71 min (self-reported "~15-20 min" — agent's clock disagreed sharply)
**Files:** 28 src + 25 test = ~53 .cs files

| Level | Score | Highlights / misses |
|---|---|---|
| L1 Structural | **15/18** | State machine, 5 events, OverdueSpec + 3 extra specs (over-scope), CancelOrder v4 pattern + handler accessor, 0 try/catch, valid CQRS, valid project structure. **Miss: L1.1** — no separate `FirstName`/`LastName` (single `CustomerName`); no `PhoneNumber` VO at all (Customer aggregate has no PhoneNumber property — spec §3.1 violation). **Miss: L1.11** — 4 spec permissions missing (`orders:ship`, `orders:deliver`, `orders:read-all`, `products:manage-stock`); renamed to `orders:fulfill` / `orders:manage-items`. **Miss: L1.18** — followed VO pattern in most places but several scope-creep deviations from the spec. |
| L2 Behavioral | **11/13** | State machine behaviors correct, cancel release, line item price snapshot, error taxonomy, order total, OverdueSpec, RequiredGuid V7, ownership-checked Cancel, batched product load (new L2.11 — 37 passing persistence tests confirm batched pattern), no handler-side `SaveChanges*` under UoW (new L2.13). **Misses: L2.10** (no PhoneNumber on Customer → `Maybe<PhoneNumber>` requirement entirely unmet); partial L2.5/L2.6 (some flows skipped due to missing permission paths). |
| L3 Architecture | **8/13** | Clean Arch, ProblemDetails, `Created()` Location, /health, 4 EntityTypeConfigurations, EnsureCreated, per-layer DI. **Misses: L3.6** — endpoint paths use `/submit` `/approve` `/ship` `/deliver` `/cancel` instead of spec's `/submission` `/approval` `/shipment` `/delivery` `/cancellation`, missing `POST /api/products/{id}/stock-additions`, missing `GET /api/customers/{id}/orders`, missing `GET /api/orders/overdue`; added `GET /api/customers/{id}` + `GET /api/products/{id}` (not in spec). **Miss: L3.7** — **no API versioning configured at all** (no `AddApiVersioning`, no `api-version` query param, no namespace-versioned controllers — flat `Api/src/Controllers/`); violates spec §7's `?api-version=2026-11-12` requirement. **Miss: L3.11** — `Models/Models.cs` not in versioned folder. |
| L4 Tests | **7/9** | 37 tests across 11 test files covering state machine, persistence (real SQLite), authorization, HTTP round-trips. **Used Trellis.Testing assertions: 53 hits of `.BeSuccess()/.BeFailure()`** — actually scored L4.9 PASS. **Miss: L4.7** — only 1 `.HaveValue()` hit; partial credit. |
| L5 Feedback | **2/4** | TRELLIS_FEEDBACK.md present but **only 680 bytes / 3 bullets** (composite VO 3-piece setup; owned-collection backing-field string; Microsoft.Testing.Platform error obscurity). **Misses: L5.2** (no category/severity/workaround/suggestion structure), **L5.3** (no "What worked well" section), **L5.4** (no Copilot-instructions feedback). |

**Failure mode:** structural divergence from the spec on three contracts (permission names, customer name shape, API versioning). Haiku's self-assessment scored L1-L5 as maturity tiers ("Met" / "Met" / "Met") rather than against the 18 sub-criteria of L1 alone, which masked the structural gaps from its own self-review. A future iteration of the prompting should explicitly walk small models through each L1 sub-criterion.

---

## Historical detailed scorecards — alpha.104 / alpha.106 era

### Claude Opus 4.6 (Copilot)

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

### GPT-5.4 (Copilot)

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

### Claude Sonnet 4.6 (Copilot)

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
