# Trellis Training Lab — Subscription Reminder Worker

> **Learn how Trellis shapes a *non-HTTP* service** — a scheduled `BackgroundService` that wakes on a timer, calls external gateways, classifies their failures, and records per-attempt idempotency state, with only a thin HTTP admin surface for inspection. It's the same framework you met in the OM lab, applied where there are no request/response cycles to hang the logic on.
>
> 🧪 Like every lab, it doubles as an [AI-consistency eval](#running-this-as-a-consistency-eval-optional). To learn, just follow the steps.

> **Do the [Order Management lab](training-lab.md) first.** It teaches the fundamentals (`Result<T>`/ROP, `Maybe<T>`, value objects, Clean Architecture, CQRS, testing) this lab assumes. There's no reference build checked in for the worker — the [spec](../specs/subscription-reminder-worker.md) and [coverage checklist](../specs/coverage-checklist-subscription-reminder.md) are the source of truth, and you learn by reading what the AI builds against them.

---

## Who this is for

A developer who's done the OM lab and wants to see Trellis on a **scheduled, autonomous** shape — the kind of service that has no controller to anchor a trace, where correctness is observed in telemetry and database state rather than an HTTP response body.

## What you'll build

A worker that, on every tick, finds subscriptions due for a renewal reminder (by tier window), sends each via the right channel (email/SMS) through a gateway, classifies the result, and records exactly one attempt per `(subscription, tier, channel)` — idempotently, so a retry never double-sends. Two small admin endpoints (`/health`, `/admin/job-runs/{id}`) expose what happened. SQLite persistence; OpenTelemetry throughout.

## What you'll learn

- How to host a **`BackgroundService` tick loop** on Trellis and keep it **testable** with `TimeProvider` (never `DateTimeOffset.UtcNow`).
- **Idempotency via a database unique constraint** on `(SubscriptionId, Tier, Channel)` — insert-then-catch, not read-then-decide — so concurrent or retried ticks never double-dispatch.
- **Classifying external-gateway failures** as transient (retry next tick → `SoftFailed`) vs. permanent (data error → `HardFailed`) off Trellis's `Error` taxonomy, instead of inventing a parallel enum.
- **Actor composition** — giving the worker a `SystemActor` **without leaking it into HTTP**, so the admin endpoints still enforce `job-runs:read`.
- Driving **domain events outside an HTTP pipeline**, and verifying behavior through **observability** (traces, metrics, structured logs) plus a counter invariant rather than a `.http` script.

---

## Core concepts you'll meet *(new vs. the OM lab)*

You already met `Result<T>`, value objects, Clean Architecture, CQRS, and testing in the [OM lab](training-lab.md#core-trellis-concepts-youll-meet). The worker shape adds:

| Concept | What it is | Why it matters here |
|---|---|---|
| **Scheduled `BackgroundService`** | A hosted service whose tick interval comes from config (`Reminders:TickIntervalMinutes`); each tick is one `JobRun`. | There's no request to scope work to — the tick *is* the unit of work, and it must be observable and idempotent on its own. |
| **`TimeProvider` everywhere** | All "is this due?" and "how old is this?" logic reads injected time. | A scheduler that reads `DateTimeOffset.UtcNow` is untestable; with `TimeProvider` you fast-forward a `FakeTimeProvider` and assert exact tick outcomes. |
| **Idempotency by unique constraint** | A DB unique index on `(SubscriptionId, Tier, Channel)`; the dispatcher **inserts then catches** the violation. | A retried or concurrent tick must not send a second reminder. Read-then-decide races; the constraint is the source of truth. |
| **Gateway error classification** | Map a gateway result to `SoftFailed` (transient, e.g. 5xx → retry next tick) or `HardFailed` (permanent, e.g. missing phone → never retry). | Lets the worker make progress on transient faults without hammering on permanent ones — and it should classify off Trellis `Error` types, not a hand-rolled `Transient`/`Permanent` enum. |
| **Actor composition (no leak)** | One `IActorProvider` that yields a `SystemActor` for the worker but a real/HTTP actor for requests. | The classic footgun: registering the worker's `SystemActor` globally so the admin endpoints stop checking permissions. The auth-composition smoke check ([6c](#6c-prove-the-actor-doesnt-leak-into-http)) proves it didn't. |
| **Counter invariant** | Every completed tick satisfies `dispatched + softFailed + hardFailed + skippedDuplicate + skippedInactive + skippedBudget == due`. | A single, cheap check that the dispatch loop accounted for every due subscription exactly once. |
| **Observability-as-verification** | No `.http` script — you read Aspire traces/metrics/logs and the two admin endpoints. | For an autonomous service, telemetry *is* the interface; building it well is part of the job, not an afterthought. |

<p align="center">
  <img src="images/architecture-overview.png" alt="Clean Architecture — API, Anti-Corruption Layer, Application, Domain" width="640"/>
</p>

---

## Prerequisites

Same as the [OM lab](training-lab.md#prerequisites). The Aspire Dashboard (OM Step 2) is **not optional here** — it's your primary window into a service with no request/response to inspect.

## The workflow

The familiar 8 steps, with worker-specific twists: the smoke test (Step 6) is **observational**, and there's **no incremental-feature step** (this lab is single-shot — see [Differences from the OM lab](#differences-from-the-order-management-lab)).

<p align="center">
  <img src="images/step-flow.png" alt="The 8-step lab workflow" width="760"/>
</p>

## Step 1 — Create a project directory

```bash
mkdir SubscriptionReminder && cd SubscriptionReminder && git init
```

## Step 2 — Start the Aspire Dashboard

Identical to [OM Step 2](training-lab.md#step-2-start-the-aspire-dashboard) — run the container once; both labs share it. You'll lean on it heavily here.

## Step 3 — Scaffold (and expect to reshape it)

```bash
dotnet new install Trellis.AspTemplate        # first time only
dotnet new trellis-asp -n SubscriptionReminder --authorName "Your Name"
dotnet build && dotnet test                    # sample tests pass
git add -A && git commit -m "Scaffold with Trellis template"
```

> **The scaffold is HTTP-CRUD-shaped; the spec is intentionally not.** The template ships a Todo HTTP sample. The worker requires deleting most of it and re-shaping the host around a `BackgroundService`. How the AI re-shapes the scaffold is part of what you're learning to recognize — don't pre-guide it. Answer any clarifying question with *"Follow the spec and copilot instructions."*

## Step 4 — Implement the service

Open Copilot Chat and **attach two files** (paperclip — don't paste): [`specs/subscription-reminder-worker.md`](../specs/subscription-reminder-worker.md) as `SPEC.md` and [`specs/coverage-checklist-subscription-reminder.md`](../specs/coverage-checklist-subscription-reminder.md) as `COVERAGE.md`. Then send:

> Implement the Subscription Renewal Reminder Worker according to the attached SPEC.md. Replace the sample Todo code — the spec is intentionally non-CRUD, so most of the template's HTTP sample should be deleted. Follow `.github/copilot-instructions.md` and `.github/trellis-api-*.md` exactly. Every row in §1–§10 of COVERAGE.md must have a matching test.

Let it work; then `dotnet build && dotnet test`, pasting back any errors until clean.

## Step 5 — Configure a fast tick and a deterministic seed

Two operator-side tweaks so the smoke test is *verifiable*:

**Fast dev tick.** The spec defaults `Reminders:TickIntervalMinutes` to 30 (production-correct). **Merge** a faster value into `appsettings.Development.json` (keep the template's other settings):

```json
{ "Reminders": { "TickIntervalMinutes": 0.5 } }
```

**Deterministic seed.** If the `DbSeeder` is random, you can't predict a tick's output. Confirm (or ask the AI to pin) a fixed seed covering at least these rows — each one teaches a branch of the dispatch logic:

| # | Channel | Phone? | Tier window | Active? | Gateway outcome | Expected first-tick result |
|---|---------|--------|-------------|---------|-----------------|----------------------------|
| 1 | Email | n/a | within 7-day | yes | success | `Dispatched` |
| 2 | Sms | yes | within 14-day | yes | success | `Dispatched` |
| 3 | Sms | **no** | within 7-day | yes | n/a (skipped) | `HardFailed` (data error — no gateway call) |
| 4 | Email | n/a | within 7-day | **no** | n/a | excluded by the due query |
| 5 | Email | n/a | within 7-day | yes | transient (5xx) | `SoftFailed` (retried next tick) |
| 6 | Email | n/a | 60 days out | yes | n/a | not due |

Expected `/health.lastTickCounts` after tick 1: `due=4, dispatched=2, softFailed=1, hardFailed=1, skippedDuplicate=0, skippedInactive=0, skippedBudget=0`. (Rows 4 and 6 are excluded by the due query, so they never appear in `due`.) If the fake gateway can't inject per-subscription outcomes, that's itself a finding — the spec (§13.3) needs it for tests, and the same hook should serve smoke.

## Step 6 — Smoke test *(observational)*

Run with telemetry pointed at the dashboard:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
dotnet run --project Api/src
```

(Bash: `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 OTEL_EXPORTER_OTLP_PROTOCOL=grpc dotnet run --project Api/src`.) Open the dashboard at http://localhost:18888 and note the app port from the console. The worker is autonomous — there's nothing to "call"; you *watch* it.

### 6a. Observability (within ~one tick)
- [ ] Startup logs report the seeded subscription count — matches the table above.
- [ ] A **"tick completed"** Information log per tick, carrying `JobRunId`, per-category counters, and `durationMs`.
- [ ] **Metrics** tab: `reminders.dispatched.total{channel=email}` increments; `reminders.tick.duration` records per tick.
- [ ] **Traces** tab: one trace per tick spanning the dispatch loop.

### 6b. Scenarios (after the first completed tick)
Find the latest `JobRunId` (Aspire Logs filtered on `JobRun`, the console, or `SELECT Id FROM JobRuns ORDER BY StartedAt DESC LIMIT 1` in `Reminders.db`). Then:
- [ ] `GET /health` → `200` with `lastTickCounts` matching `due=4, dispatched=2, softFailed=1, hardFailed=1`.
- [ ] **Counter invariant** holds: `dispatched + softFailed + hardFailed + skipped* == due`.
- [ ] `GET /admin/job-runs/{id}?api-version=1.0` with `X-Test-Actor: {"Id":"admin","Permissions":["job-runs:read"]}` → `200` with the job-run shape.
- [ ] In `DispatchAttempts`: the no-phone SMS row is `HardFailed`; the transient row is `SoftFailed`.
- [ ] **Second tick:** the transient row flips `SoftFailed → Dispatched` on the *same* attempt row — **not** a new row (proves the `(SubscriptionId, Tier, Channel)` constraint held — idempotency).

### 6c. Prove the actor doesn't leak into HTTP
Hit the admin endpoint twice — this is the check that the worker's `SystemActor` isn't leaking into HTTP (§10):

| Request | `X-Test-Actor` | Expected |
|---|---|---|
| A | `{"Id":"admin","Permissions":["job-runs:read"]}` | `200` |
| B | `{"Id":"noone","Permissions":["unrelated:perm"]}` | **`403`** (or `401`) — not `200` |

If B returns `200`, the actor provider is granting `SystemActor` (which has `job-runs:read`) to HTTP requests — a §10 violation.

### 6d. Troubleshooting
If nothing happens after a full tick: confirm the `TickIntervalMinutes` override applied (log it on startup); confirm the seed inserted rows; confirm at least one `RenewsAt` falls in a tier window from "now" (±2h per §5); confirm the app appears in Aspire's **Resources** tab within ~10s; and check the `dotnet run` console for a tick exception that was logged-and-swallowed.

## Step 7 — Review, then generate feedback

Read the generated code against [What "good" looks like](#what-good-looks-like-and-why), commit, then have Copilot produce `TRELLIS_FEEDBACK.md` (same as [OM Step 7](training-lab.md#step-7-generate-trellis-feedback)). Keep the prompt **blind** — don't name friction areas; unprompted friction is the signal. The worker shape tends to surface friction around actor composition, gateway-error classification, and testing a `BackgroundService` without `WebApplicationFactory`.

> **No Step 8.** This lab is **single-shot** — there's no incremental-feature step, so it measures initial-build understanding only (not architecture evolution).

---

## What "good" looks like (and why)

Your definition of done. (As an eval these become scored rows; the binding matrix is the [coverage checklist](../specs/coverage-checklist-subscription-reminder.md).)

- **The tick is idempotent.** One attempt row per `(SubscriptionId, Tier, Channel)`, enforced by a DB unique constraint the dispatcher **inserts-then-catches**. *Why:* retries and overlapping ticks must never double-send.
- **Time is injected.** No `DateTimeOffset.UtcNow` in production code — due-window and age logic read `TimeProvider`. *Why:* the whole service is time-driven and must be deterministically testable.
- **Failures are classified, not invented.** Transient gateway faults → `SoftFailed` (retried); permanent/data faults → `HardFailed` (not retried) — derived from Trellis `Error` types, not a parallel enum. *Why:* progress on transient faults, no thrashing on permanent ones.
- **The actor doesn't leak.** A single `IActorProvider` gives the worker a `SystemActor` while HTTP requests still resolve their own actor; `/admin/job-runs/{id}` enforces `job-runs:read`. *Why:* the dual-registration footgun silently disables admin authorization.
- **Counters reconcile.** `dispatched + softFailed + hardFailed + skipped* == due` on every completed tick, and each tick emits exactly one structured "tick completed" log with `JobRunId` + counts + `durationMs`. *Why:* an autonomous service is only as trustworthy as its telemetry.
- **Domain events flow through the pipeline**, not direct `DbContext` saves that bypass `DomainEventDispatchBehavior`.
- **Tests** cover the dispatch outcomes (dispatched/soft/hard/skipped), the idempotency constraint against real SQLite, gateway-error classification, the counter invariant, and the actor-composition rule — built on a host + `FakeTimeProvider` + SQLite fixture (there's no `WebApplicationFactory` for a worker).

---

## Differences from the Order Management lab

| Aspect | OM lab | Worker lab |
|--------|--------|------------|
| Shape | HTTP CRUD (14 endpoints) | `BackgroundService` + 2 admin endpoints |
| Smoke driver | Scripted `.http` | Observational (Aspire + 2 endpoints) |
| Time control | Per request | Scheduled; dev overrides the tick to 30s |
| Seed data | Created via API calls | `DbSeeder` on startup — no create endpoints exist |
| Resource auth | `CancelOrderCommand` owner-or-admin | None — a `SystemActor`; `job-runs:read` on the admin endpoint |
| External I/O | None | `IEmailGateway` / `ISmsGateway` (in-process fakes for the lab) |
| Incremental feature (Step 8) | Order Returns (measures architecture evolution) | **None** — single-shot; comparable to OM only for initial-build understanding |

---

## Running this as a consistency eval *(optional)*

Same methodology as the [OM lab](training-lab.md#running-this-as-a-consistency-eval-optional). The point isn't whether an AI can write a worker — it's whether **Trellis constrains independent runs to the same non-CRUD architecture.** The most informative divergence axes (each maps to a real framework-improvement issue):

| If runs diverge on… | …it tells us |
|---|---|
| One `IActorProvider` with HttpContext branching vs. the dual-registration leak | how urgently the framework needs a worker-actor-composition helper ([#529](https://github.com/xavierjohn/Trellis/issues/529)) |
| Classifying off Trellis `Error` types vs. a parallel `Transient`/`Permanent` enum | whether the `Error` taxonomy needs clearer transient/permanent guidance ([#530](https://github.com/xavierjohn/Trellis/issues/530)) |
| Hand-rolled `IHost`+`FakeTimeProvider`+SQLite fixture vs. a framework helper (none exists yet) | demand for a `BackgroundService` test harness ([#531](https://github.com/xavierjohn/Trellis/issues/531)) |
| Insert-then-catch on the unique constraint vs. read-then-decide | whether idempotency-by-constraint needs a documented recipe ([#532](https://github.com/xavierjohn/Trellis/issues/532)) |
| Saving via `DbContext` directly vs. through the domain-event pipeline | whether `DomainEventDispatchBehavior` needs better discoverability outside HTTP |

Aggregated friction across runs (from each run's `TRELLIS_FEEDBACK.md`) becomes the prioritized framework backlog.

---

## Where to go next

- The [URL Shortener](training-lab-url-shortener.md) lab — an unversioned HTTP redirect host.
- The [Order Management](training-lab.md) lab — the canonical fundamentals.
- The framework: [`xavierjohn/Trellis`](https://github.com/xavierjohn/Trellis).
