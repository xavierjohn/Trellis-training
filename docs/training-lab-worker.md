# Trellis Training Lab — Building a Background Worker with AI

> **Purpose:** Measure how consistently AI builds a non-CRUD Trellis service — specifically a scheduled `BackgroundService` that calls external gateways, persists per-attempt idempotency state, and exposes a thin HTTP admin surface. Give the AI the spec + copilot instructions and let it implement the entire worker in one shot. Run it 10 times to measure consistency, the same way the [Order Management lab](training-lab.md) does for HTTP CRUD.

This guide is the operator procedure. The binding sources are:

- [`specs/subscription-reminder-worker.md`](../specs/subscription-reminder-worker.md) — the spec.
- [`specs/coverage-checklist-subscription-reminder.md`](../specs/coverage-checklist-subscription-reminder.md) — the test surface.

The eval scoring rubric is intentionally not in this repo yet. The first lab run is itself measurement — it surfaces which rubric rows matter, and an `evaluation-criteria-subscription-reminder.md` follows once that evidence exists.

## Prerequisites

Same as the [Order Management lab Prerequisites](training-lab.md#prerequisites). No additional tools.

---

## Step 1: Create a Project Directory

Create lab runs under `C:\GitHub\Trellis-lab-runs` using the same folder shape as the OM lab:

```text
C:\GitHub\Trellis-lab-runs\<date>\run<#>\<model>\SubscriptionReminder
```

```bash
mkdir C:\GitHub\Trellis-lab-runs\2026-06-01\run1\gpt-5.5\SubscriptionReminder
cd C:\GitHub\Trellis-lab-runs\2026-06-01\run1\gpt-5.5\SubscriptionReminder
git init
```

---

## Step 2: Start the Aspire Dashboard

Identical to the OM lab — see [Step 2 of training-lab.md](training-lab.md#step-2-start-the-aspire-dashboard). Run the container once; both labs share the same dashboard.

---

## Step 3: Scaffold and Acknowledge the Non-CRUD Shape

Install the Trellis template (skip if already installed):

```bash
dotnet new install Trellis.AspTemplate
```

Scaffold the project under the lab name `SubscriptionReminder`:

```bash
dotnet new trellis-asp -n SubscriptionReminder --authorName "Your Name"
```

This produces the same scaffold as the OM lab — an HTTP-shape service with the Todo sample, `.github/copilot-instructions.md`, and the per-package API references. Verify the baseline:

```bash
dotnet build
dotnet test
```

All template tests should pass.

> **The scaffold is HTTP CRUD-shaped; the spec is intentionally not.** The template starts you with a Todo HTTP sample. The worker spec requires deleting most of it and re-shaping the host. **Do not provide additional implementation guidance during eval runs** — whether and how the AI re-shapes the scaffold is part of what the lab measures. If Copilot asks a clarifying question, answer with: "Follow the spec and copilot instructions."

Commit the bare scaffold:

```bash
git add -A
git commit -m "Scaffold with Trellis template"
```

---

## Step 4: Implement the Service

Open Copilot Chat. Attach **two files** from `Trellis-training/specs/` to the chat (paperclip icon — don't paste the bodies):

1. `specs/subscription-reminder-worker.md` as `SPEC.md`
2. `specs/coverage-checklist-subscription-reminder.md` as `COVERAGE.md`

Then send this prompt verbatim:

> Implement the Subscription Renewal Reminder Worker according to the attached SPEC.md. Replace the existing sample code (Todo) with the worker domain — the spec is intentionally non-CRUD, so most of the template's HTTP sample should be deleted. Follow `.github/copilot-instructions.md` and `.github/trellis-api-*.md` exactly. Every row in §1–§10 of COVERAGE.md must have a matching test — that file is the binding test surface, not a suggestion.

**Let the AI work.** Do not intervene unless it asks a clarifying question. If it asks, answer with: "Follow the spec and copilot instructions."

**When it finishes, verify the build:**

```bash
dotnet build
dotnet test
```

If there are build or test errors, paste them back to Copilot and let it fix them. Repeat until clean.

---

## Step 5: Configure Dev Smoke and a Deterministic Seed

**Dev tick interval.** The spec defaults `Reminders:TickIntervalMinutes` to 30 minutes (production-correct). For interactive smoke, **merge** this property into the existing `appsettings.Development.json` (keep any other settings the template already wrote, e.g. `Logging`):

```json
{
  "Reminders": {
    "TickIntervalMinutes": 0.5
  }
}
```

This is the only operator-side configuration outside the spec. Production behaviour is unchanged.

**Deterministic seed.** Smoke verification depends on knowing what each tick should produce. If the AI's `DbSeeder` is random, smoke is unverifiable. Before running Step 6, confirm the seeded data is fixed and covers at least these rows (ask the AI to add or pin them if missing):

| # | Channel | Phone? | Tier offset from now | Active? | Gateway-fake outcome | Expected first-tick result |
|---|---------|--------|----------------------|---------|----------------------|----------------------------|
| 1 | Email | n/a | within 7-day window | yes | success | `Dispatched` |
| 2 | Sms | yes | within 14-day window | yes | success | `Dispatched` |
| 3 | Sms | **no** | within 7-day window | yes | n/a (skipped) | `HardFailed` (data error — no gateway call) |
| 4 | Email | n/a | within 7-day window | **no** | n/a | not in due query (silently excluded) |
| 5 | Email | n/a | within 7-day window | yes | transient (5xx) | `SoftFailed` (retried on next tick) |
| 6 | Email | n/a | 60 days out (outside any tier window) | yes | n/a | not due, not counted |

Expected `/health.lastTickCounts` after the first tick: `due=4, dispatched=2, softFailed=1, hardFailed=1, skippedDuplicate=0, skippedInactive=0, skippedBudget=0`. The inactive row (#4) is excluded by the due query so it does not appear in `due`. The out-of-window row (#6) is also excluded.

If the AI's fake gateway interface doesn't support per-subscription outcome injection, that itself is a finding — the spec's §13.3 requires it for integration tests, so the same hook should serve dev smoke.

---

## Step 6: Manual Smoke Test

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

Open the **Aspire Dashboard** at http://localhost:18888 and note the application port from the console (e.g. `https://localhost:7234`).

The worker is autonomous — there is no `.http` script to drive it. Verification is observational. The two HTTP endpoints exist for inspection, not for triggering work.

### 6a. Observability checks (within roughly one tick interval)

- [ ] Startup logs report how many subscriptions the seeder inserted. Number matches the seed table in Step 5.
- [ ] At least one **"tick completed"** Information log appears per observed tick, carrying `JobRunId`, per-category counters, and `durationMs`. (Exact-one-per-tick is verified by the coverage checklist, not by manual smoke — Aspire log delivery is not instantaneous.)
- [ ] Aspire **Metrics** tab shows `reminders.dispatched.total{channel=email}` incrementing.
- [ ] Aspire **Metrics** tab shows `reminders.tick.duration` recording a value per tick.
- [ ] Aspire **Traces** tab shows one trace per tick spanning the dispatch loop.

### 6b. Scenario checks (after the first completed tick)

Find the latest `JobRunId` from one of these sources (in order of preference):

1. **Aspire Logs** tab — filter on `JobRun` and copy `JobRunId` from the structured "tick completed" entry.
2. **Console output** of `dotnet run` — the same structured log appears here.
3. **SQLite query** as a fallback: open `Reminders.db` and run `SELECT Id FROM JobRuns ORDER BY StartedAt DESC LIMIT 1`.

Then verify:

- [ ] `GET http://localhost:<port>/health` returns 200 with the §8 shape. `lastTickCounts` matches the expected counts from Step 5's seed table (`due=4, dispatched=2, softFailed=1, hardFailed=1`).
- [ ] **Counter invariant:** `dispatched + softFailed + hardFailed + skippedDuplicate + skippedInactive + skippedBudget == due` on every completed tick.
- [ ] `GET /admin/job-runs/{lastJobRunId}?api-version=1.0` with header `X-Test-Actor: {"Id":"admin","Permissions":["job-runs:read"]}` returns 200 with the §8 job-run shape (`id`, timestamps, `outcome`, `counts`, `failureSummary`).
- [ ] For attempt-level smoke, query `DispatchAttempts` in `Reminders.db` and verify the no-phone SMS seed row is `HardFailed` with a data-error/no-phone reason and the transient-fake seed row is `SoftFailed`.
- [ ] **Second tick** (wait another 30 seconds): `reminders.dispatched.total{channel=email}` increments by 1 (the transient row succeeds on retry) and a second `JobRun` row exists. Query `DispatchAttempts` in `Reminders.db` and verify the same `(SubscriptionId, Tier, Channel)` row moved from `SoftFailed` to `Dispatched` — **not** a new attempt row inserted (verifies §11 idempotency: the unique constraint on `(SubscriptionId, Tier, Channel)` held).

### 6c. Auth composition check (proves §10)

This is the critical check that the worker's `IActorProvider` isn't leaking `SystemActor` into HTTP. Send two requests to the same admin endpoint:

| Request | `X-Test-Actor` header | Expected |
|---------|------------------------|----------|
| A | `{"Id":"admin","Permissions":["job-runs:read"]}` | 200 OK |
| B | `{"Id":"noone","Permissions":["unrelated:perm"]}` | **403** (or 401) — **not 200** |

If request B returns 200, the worker's actor provider is granting `SystemActor` (which has `job-runs:read`) to HTTP requests — §10 violation. Note the failure for evaluation but **do not fix it during eval runs**.

If any check above fails, note it for evaluation but **do not fix it** during eval runs — it becomes your score.

### 6d. Troubleshooting

If nothing happens after one full tick interval:

- Confirm `appsettings.Development.json` actually applied the `Reminders:TickIntervalMinutes` override (log it on startup).
- Confirm the seed actually inserted subscriptions (check `Reminders.db` directly or the startup log).
- Confirm at least one seeded subscription's `RenewsAt` falls inside a tier window from "now" (with `±2 hour` tolerance per spec §5).
- Confirm the Aspire OTLP endpoint resolves: in the **Resources** tab the app should appear within 10 seconds of startup.
- Inspect the **console output** of `dotnet run` for unhandled exceptions in the worker tick — the worker may have logged-and-swallowed without surfacing to Aspire.

---

## Step 7: Review and Commit

Review the generated code against the spec and the coverage checklist. Note divergences. **Do not fix them** during eval runs — they are your scores.

Commit:

```bash
git add -A
git commit -m "Implement Subscription Renewal Reminder Worker with Trellis"
```

---

## Step 8: Generate Trellis Feedback

Ask Copilot to reflect on the development experience using the feedback format embedded in the scaffolded `.github/copilot-instructions.md` (same convention as the OM lab):

> Review the entire codebase you just built — including any framework-feedback fixes I had you apply during this run. Generate `TRELLIS_FEEDBACK.md` at the repository root following the feedback format in `.github/copilot-instructions.md` exactly: section order, field order, category/severity enums, hard constraints, and the "what to exclude" clause all apply.

The prompt is intentionally **blind** — it does not name friction areas, so the feedback file is independent observation, not prompted confirmation. Friction the AI surfaces unprompted is the measurement.

**What to verify:**

- [ ] `TRELLIS_FEEDBACK.md` exists in the repository root.
- [ ] Each friction point has a category, severity, context, and suggested improvement.
- [ ] Workaround code is included where applicable.
- [ ] "What Worked Well" section is present and specific.
- [ ] Copilot Instructions Feedback section addresses any ambiguities encountered.
- [ ] Feedback is actionable — the Trellis team can read each entry and decide whether to act on it.

```bash
git add TRELLIS_FEEDBACK.md
git commit -m "Add Trellis feedback"
```

### Post-feedback operator review (do not show this to the AI)

After the feedback file is written, the **operator** scores it against the already-filed framework-improvement issues. Recurring friction in these areas across multiple runs corroborates the priority of the corresponding issue; novel friction outside these areas is the more valuable signal — it's a gap the current issue list doesn't capture.

| Friction area to look for in `TRELLIS_FEEDBACK.md` | Tracking issue |
|----------------------------------------------------|----------------|
| Registering `BackgroundService` with the right `IActorProvider` composition (no `SystemActor` leaking into HTTP) | [xavierjohn/Trellis#529](https://github.com/xavierjohn/Trellis/issues/529) |
| Classifying gateway errors as transient vs permanent without inventing parallel enums to Trellis's `Error` taxonomy | [xavierjohn/Trellis#530](https://github.com/xavierjohn/Trellis/issues/530) |
| Building integration tests for `BackgroundService` without `WebApplicationFactory` (host + `FakeTimeProvider` + SQLite + actor + domain-event capture) | [xavierjohn/Trellis#531](https://github.com/xavierjohn/Trellis/issues/531) |
| Handling unique-constraint conflicts on `(SubscriptionId, Tier, Channel)` as idempotency signals (insert-then-catch) rather than read-then-decide | [xavierjohn/Trellis#532](https://github.com/xavierjohn/Trellis/issues/532) |

---

## Differences from the Order Management Lab

| Aspect | OM Lab | Worker Lab |
|--------|--------|------------|
| Shape | HTTP CRUD service (16 endpoints) | Background worker + 2 admin endpoints |
| Smoke driver | Scripted `.http` REST Client | Observational (Aspire Dashboard + 2 endpoints) |
| Time control | Per request | Scheduled; Development overrides tick interval to 30s |
| Seed data | Created via API calls | `DbSeeder` on startup — no creation endpoints exist (spec §14) |
| Resource auth | `CancelOrderCommand` owner-or-admin | None — single `SystemActor`; `job-runs:read` on admin endpoint |
| External I/O | None (pure in-process) | `IEmailGateway` / `ISmsGateway` — registered as in-process fakes for the lab (spec §7) |
| Feature add (Step 8 equivalent) | Returns v2 delta (measures incremental-change consistency / L6) | None yet — single-shot lab. Worker scores are comparable to OM **only for initial-build consistency (L1–L5)**, not for incremental-change consistency (L6). |

---

# Running This as an Eval

The eval methodology is the same as the OM lab. See [Running This as an Eval](training-lab.md#running-this-as-an-eval) and [Tips for Consistent Eval Runs](training-lab.md#tips-for-consistent-eval-runs) — both apply verbatim.

The point of running this lab repeatedly is **not** to measure whether the AI can write a background worker. It is to measure whether **Trellis constrains the AI enough** that 10 different runs land on the same architecture for a non-CRUD shape. Where they diverge — particularly around the measurement axes in the next table — Trellis needs a tighter building block.

## What You're Measuring

| Measurement | What divergence here tells us |
|-------------|-------------------------------|
| Did the AI register a single `IActorProvider` with HttpContext branching, or did it ship the dual-registration footgun? | Confirms / weakens urgency of #529. |
| Did the AI invent a parallel `Transient`/`Permanent` enum, or classify directly off Trellis `Error` types? | Confirms / weakens urgency of #530. |
| Did the AI build its own `IHost`+`FakeTimeProvider`+SQLite test fixture, or reuse a framework helper? (Trick question — no framework helper exists today.) | Confirms / weakens urgency of #531. |
| Did the AI `try/catch` a `DbUpdateException` to detect the unique-constraint violation, or read-then-decide? (The latter is wrong under concurrent ticks.) | Confirms / weakens urgency of #532. |
| Did the worker bypass the domain-event pipeline by saving directly through `DbContext`? | Surfaces whether `DomainEventDispatchBehavior` needs better discoverability outside HTTP. |
| Did the AI use `DateTimeOffset.UtcNow` anywhere in production code instead of `TimeProvider`? | Surfaces whether the copilot instructions emphasise time control strongly enough for non-HTTP code. |

Findings from each run flow back into `TRELLIS_FEEDBACK.md`. Aggregated friction across runs becomes the prioritised framework backlog.
