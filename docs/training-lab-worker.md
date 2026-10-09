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
- **Idempotent claim acquisition** with `TryInsertUniqueAsync` in a clean ACL context and a database unique constraint on `(SubscriptionId, Tier, Channel)`; classify `FaultCodes.DuplicateKey`, not provider exceptions in a handler.
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
| **Idempotency by unique constraint** | An ACL claim adapter calls `TryInsertUniqueAsync` on a clean context; `FaultCodes.DuplicateKey` becomes `Skipped(Duplicate)`. | The database arbitrates races. Never run the helper on a tracker containing pending `JobRun` changes. |
| **Gateway error classification** | Use `error.Classify()` and `RetryClassification`, preserving the lab's state/retry rules. | Network failures map to `Unavailable` at the gateway boundary. Opaque `TransportFault` is Permanent by default, not automatically retryable. |
| **Actor composition (no leak)** | Register the HTTP provider, then wrap it with `UseWorkerActor(systemActor)` or `AddTrellisWorkerActor`. | The shipped wrapper supplies the system actor only when there is no HttpContext; it does not authenticate anonymous HTTP requests. |
| **Durable failure state** | `Result.FailAfterCommit<T>(error)` at the leaf failure boundary; preserve that intent at the outer tick boundary. | Ordinary failures do not commit. Nested unit-of-work commits defer, and failed-result domain-event dispatch does not run. |
| **Worker harness** | `Trellis.Testing.Worker` supplies `WorkerHarness<TWorker>`, fake time and event/named-tick barriers. | No hand-written host lifecycle or sleeps. Register production Mediator/event wiring yourself; the harness registers the worker. |
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
dotnet new install Trellis.Asp.Templates@1.0.151-alpha
dotnet new trellis-asp -n SubscriptionReminder --author-name "Your Name" --api-versioning true
dotnet build && dotnet test --solution SubscriptionReminder.slnx
dotnet agentdocs check --strict
git add -A && git commit -m "Scaffold with Trellis template"
```

> **The scaffold is HTTP-CRUD-shaped; the spec is intentionally not.** The template ships a Todo HTTP sample. The worker requires deleting most of it and re-shaping the host around a `BackgroundService`. How the AI re-shapes the scaffold is part of what you're learning to recognize — don't pre-guide it. Answer any clarifying question with *"Follow the spec and copilot instructions."*

## Step 4 — Implement the service

Open Copilot Chat and **attach two files** (paperclip — don't paste): [`specs/subscription-reminder-worker.md`](../specs/subscription-reminder-worker.md) as `SPEC.md` and [`specs/coverage-checklist-subscription-reminder.md`](../specs/coverage-checklist-subscription-reminder.md) as `COVERAGE.md`. Then send:

> Implement the Subscription Renewal Reminder Worker according to SPEC.md and COVERAGE.md. Start at AGENTS.md, then the required AgentDocs router and selected recipe bodies. Replace the Todo sample. Use shipped UseWorkerActor, Error.Classify(), TryInsertUniqueAsync/FaultCodes, FailAfterCommit and WorkerHarness rather than custom equivalents. Keep claim acquisition separate from dirty tick state, preserve durable fail-fast outcomes, and test real HTTP/worker identity composition. Every row in §1–§10 of COVERAGE.md needs a matching assertion.

Let it work; then `dotnet build && dotnet test --solution SubscriptionReminder.slnx`, pasting back errors until clean. After package changes, run `dotnet restore` and `dotnet agentdocs sync` from the generated Git root.

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
| B | `{"Id":"noone","Permissions":["unrelated:perm"]}` | **`403`** — the identity exists but lacks permission |

If B returns `200`, the actor provider is granting `SystemActor` (which has `job-runs:read`) to HTTP requests — a §10 violation.

### 6d. Troubleshooting
If nothing happens after a full tick: confirm the `TickIntervalMinutes` override applied (log it on startup); confirm the seed inserted rows; confirm at least one `RenewsAt` falls in a tier window from "now" (±2h per §5); confirm the app appears in Aspire's **Resources** tab within ~10s; and check the `dotnet run` console for a tick exception that was logged-and-swallowed.

## Step 7 — Review, then generate feedback

Read the generated code against [What "good" looks like](#what-good-looks-like-and-why), commit, then have Copilot produce `TRELLIS_FEEDBACK.md` (same as [OM Step 7](training-lab.md#step-7-generate-trellis-feedback)). Keep the prompt **blind** — don't name friction areas; unprompted friction is the signal. The worker shape tends to surface friction around actor composition, gateway-error classification, and testing a `BackgroundService` without `WebApplicationFactory`.

> **No Step 8.** This lab is **single-shot** — there's no incremental-feature step, so it measures initial-build understanding only (not architecture evolution).

---

## What "good" looks like (and why)

Your definition of done. (As an eval these become scored rows; the binding matrix is the [coverage checklist](../specs/coverage-checklist-subscription-reminder.md).)

- **The tick claims idempotently.** One attempt row per triple; an ACL adapter uses `TryInsertUniqueAsync` on a clean context and translates only duplicate-key conflicts into skips. This guards ordinary retries/races, not exactly-once external delivery across process crashes.
- **Time is injected.** No `DateTimeOffset.UtcNow` in production code — due-window and age logic read `TimeProvider`. *Why:* the whole service is time-driven and must be deterministically testable.
- **Failures use `error.Classify()`.** Unavailable/RateLimited/Unexpected are Transient; AuthenticationRequired is FailFast; opaque TransportFault is Permanent. Normalize genuinely transient network faults in the gateway adapter.
- **The actor doesn't leak.** `UseWorkerActor` wraps one compatible HTTP provider; test HTTP anonymous → 401 and authenticated-without-permission → 403 separately from the harness's system actor.
- **Counters reconcile.** `dispatched + softFailed + hardFailed + skipped* == due` on every completed tick, and each tick emits exactly one structured "tick completed" log with `JobRunId` + counts + `durationMs`. *Why:* an autonomous service is only as trustworthy as its telemetry.
- **Commit boundaries are explicit.** Nested Mediator sends share the outer commit; ordinary failure must not erase attempts or the failed JobRun. Use terminal `FailAfterCommit` deliberately, and an outbox/follow-up for events that must survive failed-result suppression.
- **Tests** use `WorkerHarness` with real SQLite, fake gateways, production pipeline registrations and deterministic readiness/completion barriers. The harness owns the hosted-worker registration; HTTP composition still requires separate `WebApplicationFactory` tests.

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
| `UseWorkerActor` vs. a competing global system provider | Whether shipped composition guidance was followed |
| `Error.Classify()` vs. a copied classifier | Whether transport normalization and retry caps are understood |
| `WorkerHarness` barriers vs. sleeps or duplicate worker registration | Whether lifecycle tests exercise the shipped harness correctly |
| `TryInsertUniqueAsync` vs. provider-specific catches | Whether clean-context claim acquisition and duplicate cleanup are understood |
| Terminal `FailAfterCommit` vs. ordinary failed results | Whether the durable failure and nested commit boundaries are understood |

Aggregated friction across runs (from each run's `TRELLIS_FEEDBACK.md`) becomes the prioritized framework backlog.

---

## Where to go next

- The [URL Shortener](training-lab-url-shortener.md) lab — an unversioned HTTP redirect host.
- The [Order Management](training-lab.md) lab — the canonical fundamentals.
- The framework: [`xavierjohn/Trellis`](https://github.com/xavierjohn/Trellis).
