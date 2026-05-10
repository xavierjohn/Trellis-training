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

Create lab runs under `C:\GitHub\Trellis-lab-runs` using this folder shape:

```text
C:\GitHub\Trellis-lab-runs\<model-or-run-name>\OrderManagement
```

Use the model name plus date for normal runs, for example `gpt-5.5-2026-05-06\OrderManagement`. For repeated runs of the same model, use the existing `runN-<model>` pattern, for example `run5-gpt-5.5\OrderManagement`.

```bash
mkdir C:\GitHub\Trellis-lab-runs\gpt-5.5-2026-05-06\OrderManagement
cd C:\GitHub\Trellis-lab-runs\gpt-5.5-2026-05-06\OrderManagement
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
- `.github/trellis-api-*.md` — Per-package Trellis API surface reference
- All project files, build system (`Directory.Build.props`, `Directory.Packages.props`, `build/test.props`), and test infrastructure
- `.gitignore` configured for .NET/Visual Studio
- Working sample code (Todo) replaced with your service name

3. Verify the template builds and tests pass:

```bash
dotnet build
dotnet test
```

All 95 template tests should pass before you proceed.

4. Commit:

```bash
git add -A
git commit -m "Scaffold with Trellis template"
```

> **Why this approach?** The `dotnet new` template handles all scaffolding — project structure, build system, package references, global usings, and DI wiring. This eliminates token waste on boilerplate and ensures the AI focuses exclusively on implementing business logic. The copilot instructions (`.github/copilot-instructions.md`) tell the AI *how* to build with Trellis, and the per-package API references (`.github/trellis-api-*.md`) give it the full type surface.

---

## Step 4: Implement the Service

Open Copilot Chat. Attach **two files** from `specs/` to the chat (paperclip icon — don't paste the bodies):

1. `specs/order-management-sqlite.md` as `SPEC.md`
2. `specs/coverage-checklist.md` as `COVERAGE.md`

Then send this prompt verbatim:

> Implement the Order Management service according to the attached SPEC.md. Replace the existing sample code (Todo) with the Order Management domain. Follow `.github/copilot-instructions.md` and `.github/trellis-api-*.md` exactly. Every row in COVERAGE.md must have a matching test — that file is the binding test surface, not a suggestion.

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

Before sending requests, verify the generated service replaced the template's sample surfaces consistently:

- `api.http` uses Order Management endpoints, actors, payloads, and route names — no leftover Todo/customer-placeholder requests.
- `http-client.env.json` points at the generated API port and any auth/header variables used by `api.http`.
- `.vscode/launch.json` starts the generated API project and uses the same environment variables as the manual run command.
- Integration tests and `WebApplicationFactory` helpers target the generated host, base URL, route prefixes, and actor headers.
- Namespace/versioning/ProblemDetails metadata names the generated service, not the template sample.
- Any OpenAPI/Scalar examples or README snippets match the generated routes and DTO names.

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

1. Review all generated code against the [evaluation criteria](evaluation-criteria.md).
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

> **Attach two files** to the chat (keep `SPEC.md` and `COVERAGE.md` from Step 4 attached too):
>
> 1. `specs/order-management-returns-v2.md` as `RETURNS-V2.md` — the binding delta SDD (new API version `2026-12-01`, the new endpoint, the v2 response shape, the v1-projection rule for returned orders, the Location-header round-trip rule, and the authorization scoping).
> 2. `specs/coverage-checklist-returns.md` as `COVERAGE-RETURNS.md` — the binding test surface for the delta.
>
> The narrative below summarises the change for prompting; defer to `RETURNS-V2.md` whenever they disagree, and to `COVERAGE-RETURNS.md` whenever there's any question about which tests the implementation owes.

Paste this into Copilot Chat as a follow-up prompt (in the same conversation that built the service):

> **New requirement: Order Returns**
>
> Customers can now return delivered orders. Implement the delta described in `RETURNS-V2.md` on top of the existing service:
>
> **Scope reminder (full rules in `RETURNS-V2.md`):**
> - Ships under a new API version `2026-12-01`. The existing 16 endpoints from Steps 1–7 stay on `2026-11-12` AND must also be served under `2026-12-01`.
> - `POST /api/orders/{id}/return` is v2-only. Calling it under `?api-version=2026-11-12` returns 404.
> - v1 response shape is unchanged: no `returnReason`/`returnedAt`, returned orders project as `status: "Delivered"`. v2 response adds `returnReason` and `returnedAt` and exposes `status: "Returned"`.
> - `Location` headers round-trip the requested api-version. Do not hardcode a version.
> - `orders:return` is required only for the new endpoint, not for v1 operations served under v2.
>
> **Domain changes:**
> - Add `Returned` to the OrderStatus enum
> - Add a `ReturnReason` value object — required string, 10–500 characters
> - Add `ReturnReason` as a property on the Order aggregate (absent until returned, persisted to database, included in API responses **on v2 only**)
> - Add `ReturnedAt` as a `Maybe<DateTime>` property on Order (set during Return transition; included in API responses **on v2 only**)
> - (`DeliveredAt` is already a base property — set during the Shipped → Delivered transition. Do not redefine it.)
> - Add state transition: `Delivered → Returned`
>   - Precondition: Order must have been delivered within the last 30 days. **Use the injected `TimeProvider`** (do not call `DateTime.UtcNow`). Valid when `now - DeliveredAt <= TimeSpan.FromDays(30)`; invalid when greater than 30 days. Both `now` and `DeliveredAt` are UTC instants from `TimeProvider`.
>   - Side effect: Release reserved stock for each line item (same as cancel)
>   - Side effect: Set `ReturnedAt` to current UTC time (from `TimeProvider`)
>   - Side effect: Set `ReturnReason` on the order
>   - Domain event: `OrderReturnedEvent(OrderId, CustomerId, ReturnReason, ReturnedAt)`
> - Shipped and Cancelled orders cannot be returned
> - Already-returned orders cannot be returned again
>
> **Application changes:**
> - Add `ReturnOrderCommand` with `orders:return` permission
> - **Resource-based authorization (mirror Cancel Order):** the actor must be the order creator OR have `orders:read-all` (admin). Non-owner non-admin → `Error.Forbidden` (403).
> - Add `ReturnOrderHandler` — fetches order + products, validates return window via injected `TimeProvider`, fires transition, releases stock, saves
> - Add permission: `orders:return` to Permissions class
> - SalesRep role gets `orders:return` permission
>
> **API changes:**
> - Add endpoint: `POST /api/orders/{id}/return` with body `{ "reason": "..." }`, accessible only on `?api-version=2026-12-01` (see `RETURNS-V2.md` §3.1)
> - Status codes: 200 OK on success, 422 on window expired/invalid transition, 403 on missing permission or non-owner non-admin, 404 on order not found OR called under `?api-version=2026-11-12`
>
> **Test changes:**
> - Domain: return within window succeeds, return at exactly 30 days succeeds (boundary inclusive), return after 30 days fails, return from non-Delivered status fails, stock released on return
> - Application: handler happy path, missing permission, non-owner non-admin → 403, owner succeeds, admin succeeds
> - API (Step 8 endpoint): HTTP round-trip for successful return, 422 for expired window, 403 for non-owner
> - API (multi-version regression): every v1 endpoint must work under both `?api-version=2026-11-12` and `?api-version=2026-12-01`. v2 returns the new fields; v1 must NOT include them. `POST /api/orders/{id}/return?api-version=2026-11-12` returns 404. Location headers round-trip the requested api-version.
>
> **Storage changes:**
> - SQLite/EF: add `ReturnedAt` as a `partial Maybe<DateTime>` property on Order — the source generator and `MaybeConvention` handle persistence automatically. Persist `ReturnReason` via the existing value-object pattern. (Cosmos: serialize via the Cosmos SDK as nullable JSON properties.)
>
> **Coverage:** every row in `COVERAGE-RETURNS.md` must have a matching test in addition to keeping `COVERAGE.md` green. §R7 (Multi-version conformance) is the binding test surface for the cross-version requirements.

### What This Tests

This exercise specifically validates that:

| What | Why It Matters |
|------|---------------|
| **State machine modification** | Can the AI add a new status + transition to an existing `Trellis.StateMachine` without breaking existing transitions? |
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
- [ ] `ReturnReason` persisted as a property on Order (absent until returned, included in API response)
- [ ] `DeliveredAt` is set during Delivered transition (already a base property)
- [ ] `ReturnedAt` is `Maybe<DateTime>` on Order, set during Return transition
- [ ] State machine allows `Delivered → Returned` only
- [ ] Return checks 30-day window from `DeliveredAt` using injected `TimeProvider`
- [ ] Stock release runs on return (same as cancel from Submitted/Approved)
- [ ] `OrderReturnedEvent` raised with reason
- [ ] `orders:return` permission added to Permissions class
- [ ] `ReturnOrderCommand` implements `IAuthorize` AND `IAuthorizeResource` (owner OR admin)
- [ ] `POST /api/orders/{id}/return` endpoint exists with correct versioning
- [ ] Domain tests cover: valid return, 30-day boundary inclusive, expired window, invalid source status
- [ ] API test covers HTTP round-trip + 403 for non-owner

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
2. **Follow Steps 1–8 identically** in each session.
3. **After each session,** score the output against the [Evaluation Criteria](evaluation-criteria.md).
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

## Evaluation

See [Evaluation Criteria](evaluation-criteria.md) for the scoring rubric, tracking tables, and consistency measurement methodology.
