# Subscription Renewal Reminder Worker — Specification

> This specification describes a background-worker service that dispatches subscription renewal reminders. Use template 1.0.151-alpha / Trellis alpha.557 and start at AGENTS.md, the required AgentDocs router and selected recipe bodies. Preserve the business rules below while adopting shipped worker, persistence, authorization and testing helpers.
>
> **This lab is deliberately non-CRUD.** The service is dominated by a scheduled `BackgroundService` rather than HTTP request handlers. Only a thin HTTP surface (health probe + one read-only admin endpoint) exists. The spec exists to stress Trellis primitives — `Result<T>`, `IActorProvider`, `Maybe<T>`, `Specification<T>`, CQRS — in a context where there is no incoming HTTP request, no response writer, and no per-request scope managed by ASP.NET. Implementations should compose framework primitives without inventing per-feature adapters where a generic abstraction already exists.

## 1. Domain Overview

A SaaS company sells time-bounded subscription plans. Each subscription has a renewal date. Before that date arrives, the subscriber should receive one or more reminders so they can confirm, cancel, or update payment.

A background worker runs on a fixed schedule. On each tick, it loads subscriptions whose next renewal date falls inside a configured reminder window, computes which reminder tiers are due, and dispatches a notification through the subscriber's preferred channel. Each dispatch is recorded as a persistent attempt with status, retry count, and provider message id. Failed dispatches are categorised by cause: transient failures retry on subsequent ticks up to a cap; permanent failures terminate; some data conditions short-circuit an individual dispatch. A few conditions (e.g., the gateway reports the API key is revoked) short-circuit the whole tick.

The worker runs unattended. There is no human user behind any operation. Authorisation uses a single `SystemActor` identity. A thin HTTP surface exposes a health probe (anonymous) and a read-only admin view of past job runs (permission-gated). All other behaviour is internal.

## 2. Ubiquitous Language

| Term | Definition |
|------|------------|
| **Subscription** | A time-bounded plan a subscriber is currently on. Has a renewal date. Identified by a unique id. |
| **Subscriber** | The person who owns one or more subscriptions. Has an email address and an optional phone number. |
| **Plan** | The product the subscriber is on (e.g., "Basic Monthly", "Pro Annual"). Plan name and price are captured on the subscription at creation. |
| **Reminder Tier** | A fixed offset before the renewal date at which a reminder is sent. The spec defines four tiers: 30 days, 14 days, 7 days, and 1 day before renewal. |
| **Notification Channel** | The transport used to deliver a reminder. Two channels are supported: `Email` and `Sms`. |
| **Dispatch Attempt** | A single record of "we tried to send tier X over channel Y for subscription Z." Has a status, attempted-at timestamp, retry count, optional provider message id, and optional failure reason. |
| **Job Run** | One execution of the worker tick. Has a started-at timestamp, an outcome, and per-category counters (dispatched, soft-failed, hard-failed, skipped-duplicate). |
| **Reminder Window** | A wall-clock tolerance around the tier-anchored time. A reminder is "due" if `now` falls within `[renewsAt − tier − window/2, renewsAt − tier + window/2]`. Prevents missed reminders when ticks run slightly late. |
| **Soft Failure** | A dispatch failure caused by a transient condition (gateway 5xx, timeout, throttling). The attempt is recorded as `SoftFailed` and is retried on the next tick. |
| **Hard Failure** | A dispatch failure caused by a permanent condition (invalid address, gateway rejection, retries exhausted). The attempt is recorded as `HardFailed` and is never retried. |
| **Fail-Fast** | A condition that aborts the entire tick (e.g., gateway returns 401 — API key revoked). The `JobRun` is marked `Failed` and the worker logs at error severity. |
| **Idempotency Key** | The composite `(SubscriptionId, Tier, Channel)`. Duplicate attempts under the same key are rejected at the persistence layer and counted as `SkippedDuplicate` (not failures). |
| **System Actor** | The single non-human identity under which all worker operations run. Holds permissions `reminders:dispatch`, `reminders:read`, `job-runs:read`. |

## 3. Aggregates

### 3.1 Subscription Aggregate

**Identity:** `SubscriptionId` (unique identifier).

**Properties:**
- `SubscriberId` — required identifier of the owning subscriber
- `PlanId` — required identifier of the plan
- `PlanName` — required, 1–100 characters (captured at creation time, not a navigation property)
- `MonthlyPrice` — `Money` (positive, currency)
- `SubscriberEmail` — required, validated via Trellis `EmailAddress`
- `SubscriberPhone` — optional, `Maybe<PhoneNumber>` (validated via Trellis `PhoneNumber` / E.164)
- `PreferredChannel` — `NotificationChannel` enum (`Email` or `Sms`)
- `StartedAt` — required UTC timestamp
- `RenewsAt` — required UTC timestamp, must be strictly after `StartedAt`
- `IsActive` — boolean; when false, the subscription is excluded from all reminder dispatch

**Rules:**
- A subscription cannot be created with `RenewsAt <= StartedAt`.
- A subscription with `PreferredChannel = Sms` and `SubscriberPhone = None` is permitted at creation time, but the worker will produce a `HardFailed` dispatch attempt categorised as a data error rather than calling any gateway.
- `IsActive` may be flipped to false at any time; the worker re-checks it just before each dispatch.

**Operations:**
- `MarkInactive()` — sets `IsActive` to false. Idempotent.

### 3.2 DispatchAttempt Aggregate

**Identity:** `DispatchAttemptId` (unique identifier).

**Properties:**
- `SubscriptionId` — reference to the subscription this attempt targets
- `Tier` — `ReminderTier` enum (`T30`, `T14`, `T7`, `T1`)
- `Channel` — `NotificationChannel` enum
- `Status` — `DispatchStatus` enum (`Pending`, `Dispatched`, `SoftFailed`, `HardFailed`)
- `RetryCount` — non-negative integer; starts at 0; incremented on each retry tick
- `MaxRetries` — fixed at 5 (constant; not stored per row)
- `FirstAttemptedAt` — required UTC timestamp
- `LastAttemptedAt` — required UTC timestamp
- `CompletedAt` — `Maybe<DateTimeOffset>` (set when status reaches `Dispatched` or `HardFailed`)
- `ProviderMessageId` — `Maybe<ProviderMessageId>` (string 1–200 chars; set when `Dispatched`)
- `FailureReason` — `Maybe<FailureReason>` (string 1–500 chars; set when `SoftFailed` or `HardFailed`)

**Rules:**
- The triple `(SubscriptionId, Tier, Channel)` is unique system-wide. A second attempt for the same triple is rejected by the persistence layer and counted as a `SkippedDuplicate` at the tick level (not an error).
- `Status` mutations follow the state machine in §4.

**Operations:**
- `MarkDispatched(ProviderMessageId, completedAt)` — `Pending` → `Dispatched`
- `RecordSoftFailure(reason, attemptedAt)` — `Pending` → `SoftFailed`; increments `RetryCount`. If `RetryCount` reaches `MaxRetries`, escalates to `HardFailed` instead.
- `RecordHardFailure(reason, completedAt)` — `Pending` → `HardFailed`
- `ResetForRetry(attemptedAt)` — `SoftFailed` → `Pending` (called by the orchestrator at the start of the retry tick before invoking the gateway)

### 3.3 JobRun Aggregate

**Identity:** `JobRunId` (unique identifier).

**Properties:**
- `StartedAt` — required UTC timestamp
- `CompletedAt` — `Maybe<DateTimeOffset>`
- `Outcome` — `JobRunOutcome` enum (`Running`, `Succeeded`, `PartiallyFailed`, `Failed`)
- `DueCount` — non-negative integer
- `DispatchedCount` — non-negative integer
- `SoftFailedCount` — non-negative integer
- `HardFailedCount` — non-negative integer
- `SkippedDuplicateCount` — non-negative integer (composite-key collision on insert)
- `SkippedInactiveCount` — non-negative integer (subscription became inactive or vanished between query and dispatch)
- `SkippedBudgetCount` — non-negative integer (items returned by the due query that the tick did not attempt because the wall-clock budget was exhausted or the worker was cancelled)
- `FailureSummary` — `Maybe<string>` (1–500 chars; set by `FailFast`; counter-derived failure can leave it absent)

**Counter invariant.** `DispatchedCount + SoftFailedCount + HardFailedCount + SkippedDuplicateCount + SkippedInactiveCount + SkippedBudgetCount = DueCount` on every completed tick (normal, budget-exhausted, cancelled, and fail-fast). Fail-fast and cancellation contribute remaining unattempted items to `SkippedBudgetCount` so the invariant always holds.

**Rules:**
- `Outcome` defaults to `Running`; finalised on completion.
- `Outcome = Failed` iff `FailureSummary` is set (fail-fast condition reached) **or** `DispatchedCount == 0 && (HardFailedCount + SoftFailedCount) > 0` (all attempted items failed), even when budget/cancellation skips remain.
- `Outcome = Succeeded` iff `SoftFailedCount = 0 && HardFailedCount = 0 && SkippedBudgetCount = 0 && FailureSummary = None`. `SkippedDuplicate` and `SkippedInactive` do not block `Succeeded` — they are no-ops, not failures. `SkippedBudget > 0` does block `Succeeded` because the tick demonstrably did not finish its work.
- `Outcome = PartiallyFailed` iff `FailureSummary = None && (DispatchedCount > 0 || (SoftFailedCount + HardFailedCount) == 0) && (SoftFailedCount + HardFailedCount + SkippedBudgetCount) > 0`. Thus an entirely unattempted batch is partially failed, but a failed attempted prefix with zero dispatches remains failed.

**Operations:**
- `IncrementDispatched()`, `IncrementSoftFailed()`, `IncrementHardFailed()`, `IncrementSkippedDuplicate()`, `IncrementSkippedInactive()`, `IncrementSkippedBudget(count)` (the budget bump is by the remaining unattempted count, not always one)
- `Complete(completedAt)` — sets `CompletedAt` and derives `Outcome` from counters
- `FailFast(reason, completedAt)` — sets `Outcome = Failed`, `FailureSummary = reason`, `CompletedAt = completedAt`

## 4. State Machine

The `DispatchAttempt` state machine is deliberately compact. There is no rich domain workflow — the focus of this lab is the *orchestration around* the attempt, not the attempt itself.

```
                  ┌───────────────┐
                  │    Pending    │ ◄─── (initial)
                  └───────────────┘
                          │
              ┌───────────┼────────────┐
              ▼           ▼            ▼
       ┌────────────┐ ┌──────────┐ ┌────────────┐
       │ Dispatched │ │SoftFailed│ │ HardFailed │
       └────────────┘ └──────────┘ └────────────┘
        (terminal)        │         (terminal)
                          │
                          ▼
                 (on next tick: ResetForRetry → Pending)
```

**Transitions:**

| From | To | Trigger | Side effect |
|------|----|---------|-------------|
| `Pending` | `Dispatched` | Gateway returns `Result.Ok(messageId)` | Sets `ProviderMessageId`, `CompletedAt`. Raises `ReminderDispatchedDomainEvent`. |
| `Pending` | `SoftFailed` | Gateway failure mapped to "transient" (see §7) AND `RetryCount < MaxRetries - 1` (i.e., another retry is still available) | Sets `FailureReason`, `LastAttemptedAt`. Increments `RetryCount`. Raises `ReminderSoftFailedDomainEvent`. |
| `Pending` | `HardFailed` | Gateway failure mapped to "permanent" (see §7) OR ("transient" AND `RetryCount >= MaxRetries - 1`, i.e., this would be the final retry) OR subscriber data error (e.g., SMS preferred but `SubscriberPhone = None`) | Sets `FailureReason`, `CompletedAt`. Raises `ReminderHardFailedDomainEvent`. |
| `SoftFailed` | `Pending` | Next worker tick selects this attempt for retry | Updates `LastAttemptedAt`. No event. |

`MaxRetries = 5` means there are at most five total gateway attempts per `(SubscriptionId, Tier, Channel)` triple. The fifth transient failure becomes `HardFailed` with reason `"transient retries exhausted"` rather than `SoftFailed`.

**Invalid transitions** (all return `Result.Fail(Error.InvalidInput)`):
- `Dispatched → *` — terminal
- `HardFailed → *` — terminal
- `SoftFailed → Dispatched` directly (must go through `Pending`)
- `Pending → Pending` (no-op rejected)

## 5. Worker Schedule

The worker is implemented as a single `BackgroundService` (or equivalent `IHostedService`) registered on the host.

**Schedule:**
- Tick interval: **30 minutes** (configurable via `Reminders:TickIntervalMinutes`; default 30).
- Reminder tiers: **30, 14, 7, 1 days** before `RenewsAt` (configurable; default these four).
- Reminder window: **±2 hours** around the tier-anchored time. (Tolerance for late ticks.)
- Per-tick batch limit: **500 subscriptions** (configurable via `Reminders:MaxBatchSize`).
- Per-tick wall-clock budget: **60 seconds** (configurable via `Reminders:TickBudgetSeconds`).

**Tick lifecycle:**

1. Open a fresh DI scope for the tick; resolve Mediator and create/stage `JobRun`.
2. Keep persistence boundaries explicit (§6.1); unique attempt claims use a separate clean ACL context.
3. The standard authorization pipeline resolves the worker actor. Actor-aware handlers receive that checked actor; do not resolve it again in their business body.
4. Query due reminders (§6.3). The query returns at most `MaxBatchSize` items.
5. For each due item, dispatch in sequence (see §6.2). Update `JobRun` counters in-place after each item. Honour the wall-clock budget — if exceeded mid-batch, stop dispatching, call `IncrementSkippedBudget(remainingCount)` so the counter invariant holds, then finalise via `Complete(...)` using §3.3's mutually exclusive outcome rules.
6. On fail-fast, count the offending attempt's resulting SoftFailed/HardFailed state, add unattempted items to SkippedBudget, call JobRun.FailFast and preserve terminal persist-on-failure intent.
7. On normal completion, call `JobRun.Complete(...)`. Persist.
8. Emit one structured log entry summarising the tick (see §9). Emit metric updates.

**Time source:** the worker must obtain "now" from an injected `TimeProvider` (the .NET 8 abstraction, available via `IServiceCollection.AddSingleton(TimeProvider.System)`). Direct calls to `DateTimeOffset.UtcNow` are not permitted because they cannot be controlled in tests.

**Cancellation:** observe the worker stop token in work/gateway calls. Finalize counters and attempt state with a separate bounded cleanup token, not the already-cancelled work token; derive Outcome via `Complete`. A cancelled outer dispatch cannot rely on `TransactionalCommandBehavior` to save itself. Provide an explicit finalization boundary and prove durable prior outcomes in SQLite; do not leave a partially processed attempt Pending or silently claim cleanup succeeded when saving failed.

## 6. Operations (Use Cases)

All operations are implemented as Commands or Queries using CQRS. The worker is not an HTTP request handler — it is the consumer of `RunJobTickCommand`. The two HTTP endpoints (§8) call the relevant Query.

### 6.1 Run Job Tick (Command)

- **Caller:** the `BackgroundService` on each tick.
- **Permission required:** `reminders:dispatch` (granted to `SystemActor`).
- **Input:** `tickStartedAt` (UTC timestamp captured from `TimeProvider`).
- **Behaviour:** orchestrates §5. Creates `JobRun`, queries due reminders, dispatches each (via `DispatchReminderCommand`), persists final `JobRun`.
- **Success:** `Result.Ok(JobRunId)`.
- **Failure:** fail-fast returns the original gateway Error via terminal `Result.FailAfterCommit<JobRunId>(error)` after staging the failed JobRun and attempt state. Ordinary `Result.Fail` would roll back required state.

**Commit ownership matters.** Nested Mediator commands on the same scoped unit of work defer their commits to the outer tick. Preserve persist-on-failure intent at both leaf and outer boundaries; an inner marker alone does not make the outer tick commit. Do not fold such results through `Combine`/`TraverseAll`. A failed commit replaces the original error and must be surfaced. Prior successful dispatches and final counters must survive fail-fast; cancellation needs the explicit cleanup boundary above. Unique claims are separately durable, so this is not an exactly-once guarantee for an external send across process crashes.

### 6.2 Dispatch Reminder (Command)

- **Caller:** `RunJobTickCommand` per due item.
- **Permission required:** `reminders:dispatch`.
- **Input:** `SubscriptionId`, `Tier`, `Channel`, `ExistingAttemptId: Maybe<DispatchAttemptId>` (populated by §6.3 when the due item is a retry of an existing `SoftFailed` attempt; `None` for a first attempt).
- **Behaviour:**
  1. **Load subscription.** If not found or `IsActive = false`, return `Result.Ok(DispatchOutcome.Skipped(SkipReason.NoLongerEligible))` — not an error.
  2. **Acquire the attempt.**
     - If `ExistingAttemptId` is `Some(id)` → load by id. If status is not `SoftFailed`, treat as a race (another tick processed it) and return `Result.Ok(DispatchOutcome.Skipped(SkipReason.Duplicate))`. Otherwise call `ResetForRetry(now)` to flip status to `Pending`.
     - If `ExistingAttemptId` is `None` → acquire a Pending attempt using an ACL adapter and `TryInsertUniqueAsync` in a clean context. Match only `Error.Conflict.Code == FaultCodes.DuplicateKey` to return `Skipped(Duplicate)`; a competing insert won the race.
  3. **Check data preconditions.** If `Channel = Sms` and `SubscriberPhone = None`, call `RecordHardFailure(reason: "no phone on file", completedAt: now)` on the attempt acquired in step 2, persist, and return `Result.Ok(DispatchOutcome.HardFailed(DataError))`. Do **not** call the gateway.
  4. **Call the gateway** (`IEmailGateway` or `ISmsGateway`).
  5. **Translate the gateway `Result<ProviderMessageId>`** into the attempt state via the state machine (§4) and the error classification (§7). Persist.
     - **FailFast** (e.g., AuthenticationRequired): record the authentication failure on the acquired attempt, never leave it Pending. `RecordSoftFailure` leaves it SoftFailed below the cap, or HardFailed at the fifth attempt per §4. Return terminal `Result.FailAfterCommit<DispatchOutcome>(originalError)` and preserve that intent at the tick boundary.
- **Success:** `Result.Ok(DispatchOutcome)` where `DispatchOutcome` is one of `Dispatched(messageId) | SoftFailed | HardFailed(category: PermanentGatewayError | RetriesExhausted | DataError) | Skipped(reason)`.
- **Failure:** only FailFast produces a failed Result (with persist-on-failure intent). Per-item soft/hard outcomes are successful Result payloads so iteration continues. Attempt state is staged, not necessarily committed until the owning tick returns; no processed row remains Pending afterward.

`TryInsertUniqueAsync` immediately saves a single claim and requires a clean tracker. Never call it with a pending JobRun or unrelated attempt mutations; use a factory-created ACL context. Duplicate cleanup is shipped behavior; FK/concurrency/infrastructure/cancellation failures propagate, not duplicate skips. Retries load by the supplied existing attempt ID.

### 6.3 Query Due Reminders (Query)

- **Permission required:** `reminders:read`.
- **Input:** `now` (UTC), `maxBatchSize`.
- **Behaviour:** returns a list of `DueReminder { SubscriptionId, Tier, Channel, ExistingAttemptId: Maybe<DispatchAttemptId> }` where:
  - The subscription is active.
  - The tier's anchor time falls within the reminder window around `now`.
  - For the triple `(SubscriptionId, Tier, Channel)` — which is unique system-wide per the index on `DispatchAttempts` — the existing attempt (if any) determines inclusion: no attempt → include with `ExistingAttemptId = None`; existing attempt in `SoftFailed` → include with `ExistingAttemptId = Some(id)` (the worker will call `ResetForRetry`); existing attempt in `Dispatched` or `HardFailed` → **excluded**.
- **Implementation:** two concerns, separately exercised:
  - **`DueSubscriptionWindowSpecification`** — a `Specification<Subscription>` capturing the active + window-overlap predicate. Must translate to SQL and evaluate in-memory.
  - **Attempt-state join** — a query (not a specification) that left-joins the windowed subscription set against `DispatchAttempts` on the triple, filters in/out by attempt status, and produces the `DueReminder` projection with `ExistingAttemptId`. The unique index guarantees at most one attempt per triple, so no "pick most recent" tie-breaking is required.
- **Output:** ordered by `RenewsAt` ascending; capped at `maxBatchSize`.

### 6.4 Fail-Fast Conditions

The orchestrator (`RunJobTickCommand`) halts the tick and marks `JobRun.Outcome = Failed` when:

- Any gateway call returns `Result.Fail(Error.AuthenticationRequired)` (e.g., API key revoked). Continuing would generate cascading failures and waste retry budget.
- Database `SaveChangesAsync` raises an `OperationCanceledException` not originating from the worker's own stop token.

Per-item transient and permanent gateway failures **do not** fail-fast. Continue processing and derive the final outcome from §3.3: `Failed` if no item dispatched and any attempt failed; `PartiallyFailed` for mixed dispatch/failure or otherwise unfinished work; `Succeeded` if there are no failures or budget skips.

### 6.5 Query Job Run By Id (Query)

- **Permission required:** `job-runs:read`.
- **Input:** `JobRunId`.
- **Behaviour:** loads the `JobRun` projection (counters + outcome + timestamps + optional failure summary).
- **Output:** `Result.Ok(JobRunView)` on success; `Result.Fail(Error.NotFound)` if absent.

### 6.6 Health Query

- **Permission required:** none (anonymous).
- **Behaviour:** returns `{lastTickAt, lastTickOutcome, lastTickCounts}` derived from the most recent persisted `JobRun`. If no `JobRun` has been persisted yet, returns `{lastTickAt: null, lastTickOutcome: "never-run"}`.

## 7. External Gateway Contracts

External notification providers are abstracted behind two interfaces. The implementations called by the worker are deterministic fakes (for the lab); a production app would substitute real provider clients without changing the orchestrator.

```csharp
public interface IEmailGateway
{
    Task<Result<ProviderMessageId>> SendAsync(
        EmailAddress to,
        EmailMessage message,
        CancellationToken cancellationToken);
}

public interface ISmsGateway
{
    Task<Result<ProviderMessageId>> SendAsync(
        PhoneNumber to,
        SmsMessage message,
        CancellationToken cancellationToken);
}
```

**Error categorisation.** Both gateways return `Result<ProviderMessageId>`. Implementations must use **existing Trellis `Error` types** (do not invent project-local "Transient/Permanent" enums or `Error.External.*` subtypes — Trellis's taxonomy is already transport-neutral and covers the necessary retry semantics). The orchestrator classifies each error into one of three categories:

| Gateway condition | Trellis `Error` returned by the gateway | Orchestrator category | Mapped to dispatch status |
|-------------------|-----------------------------------------|-----------------------|---------------------------|
| Network timeout, 5xx, retryable DNS/TLS/reset | `new Error.Unavailable(retryAdvice) { Code = "gateway.unavailable" }` | **Transient** | `SoftFailed` below the five-attempt cap; `HardFailed` on exhaustion |
| Throttling (429 with `Retry-After`) | `Error.RateLimited(RetryAdvice)` | **Transient** | Same as above. The orchestrator may honour `RetryAdvice.After` to delay the next tick's attempt, but a fixed-interval scheduler that simply lets the next tick retry is also acceptable. |
| Unexpected gateway exception / malformed response | `Error.Unexpected(reasonCode, faultId?)` | **Transient** | Same as `Unavailable`. |
| Invalid address | `Error.InvalidInput.ForField("gateway.address.invalid", "recipient", detail: "...")` | **Permanent** | `HardFailed` |
| Content rejected, recipient blocked, business-rule violation in gateway | `Error.InvariantViolation(reasonCode, resource?)` | **Permanent** | `HardFailed` (terminal). |
| Authentication failure (API key revoked, signature rejected) | `Error.AuthenticationRequired(scheme?)` | **Fail-fast** | Halts tick; `JobRun.Outcome = Failed`. |
| Recipient blocked by policy | `Error.Forbidden(code, resource?)` | **Permanent** | `HardFailed`; credentials are valid but recipient is refused |
| Opaque transport fault | `Error.TransportFault(fault)` | **Permanent** | `HardFailed`; normalize genuinely retryable network failures to Unavailable at the gateway boundary |

Call the shipped `error.Classify()` and switch on `RetryClassification`; do not copy the closed Error taxonomy into a local classifier/enum. Honor retry caps and optional advice in domain orchestration. Catch unexpected gateway exceptions at the adapter boundary and map to safe `Error.Unexpected`; do not swallow cancellation or expose exception messages as public detail.

## 8. HTTP Surface

This service exposes exactly two HTTP endpoints. They exist for operational visibility, not for the core workflow.

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| GET | `/health` | anonymous | Liveness + last-tick status |
| GET | `/admin/job-runs/{id}?api-version=1.0` | requires `job-runs:read` | Read-only view of one past job run |

**Response shape — `GET /health`:**

```json
{
  "status": "healthy",
  "lastTickAt": "2025-01-15T03:00:00Z",
  "lastTickOutcome": "Succeeded",
  "lastTickCounts": {
    "due": 142,
    "dispatched": 138,
    "softFailed": 2,
    "hardFailed": 1,
    "skippedDuplicate": 1,
    "skippedInactive": 0,
    "skippedBudget": 0
  }
}
```

If no tick has run yet, return `200 OK` with `{"status": "healthy", "lastTickAt": null, "lastTickOutcome": "never-run"}`. (The worker being up but having never ticked is a normal startup state.)

**Response shape — `GET /admin/job-runs/{id}`:**

```json
{
  "id": "...",
  "startedAt": "2025-01-15T03:00:00Z",
  "completedAt": "2025-01-15T03:00:12Z",
  "outcome": "Succeeded",
  "counts": { ... },
  "failureSummary": null
}
```

Admin errors: 401 absent identity, 403 missing permission, 404 absent run, 400 missing version, 422 invalid typed input. Use `UseAsp()` / Result response mapping plus `UseProblemDetails()` and standard exception/status-code middleware; ProblemDetails enrichment does not own Result-to-HTTP mapping. ETag is not required.

The admin GET uses the versioned HTTP profile; no Location header or version-aware Location chain is needed for a read. Keep its explicit query-string version reader and test identity separately from worker dispatch.

## 9. Result Reporting & Observability

The worker emits three observability surfaces. None of them go through `ResponseFailureWriter` (no HTTP response to write into).

### 9.1 Structured Logs

Per tick (one entry, at Information):

```
JobRun {JobRunId} completed: outcome={Outcome}, due={DueCount},
dispatched={DispatchedCount}, softFailed={SoftFailedCount},
hardFailed={HardFailedCount}, skippedDuplicate={SkippedDuplicateCount},
skippedInactive={SkippedInactiveCount}, skippedBudget={SkippedBudgetCount}, duration={DurationMs}ms
```

Per dispatch (one entry, level depends on outcome):
- `Dispatched` → Debug
- `SoftFailed` → Warning, with `RetryCount`, `Provider`, `Reason`
- `HardFailed` → Error, with `Reason`
- `Skipped(*)` → Information, with skip reason

Fail-fast (one entry, Error): `JobRun {JobRunId} aborted: reason={FailureSummary}`.

All log entries use **structured logging placeholders** (`{Name}`) rather than string interpolation, so `JobRunId`, `Status`, `Provider`, `Reason`, `RetryCount`, and other fields are emitted as named properties on each log entry (not embedded in the message text). The recommended mechanism is `ILogger.BeginScope` to attach `JobRunId` to all per-item entries within a tick.

### 9.2 Metrics

A `Meter` named `Reminders` exposes:

| Instrument | Type | Description |
|------------|------|-------------|
| `reminders.tick.duration` | Histogram (seconds) | Wall-clock time per completed tick |
| `reminders.dispatched.total` | Counter | Successful dispatches, tagged `channel` |
| `reminders.failed.total` | Counter | Failed dispatches, tagged `channel`, `category` (`transient` / `permanent` / `data-error` / `retries-exhausted`) |
| `reminders.skipped.total` | Counter | Skipped items, tagged `reason` (`duplicate` / `inactive`) |
| `reminders.batch.size` | Histogram | Items processed per tick |

### 9.3 Persisted Job Run

The `JobRun` aggregate (§3.3) is the durable forensic record. Logs may be dropped or sampled; metrics aggregate; only `JobRun` is the source of truth for "what happened on tick T".

## 10. Authorization

There is no human user behind a worker tick. There is a human user behind a request to `/admin/job-runs/{id}`. Both paths run in the same process and share a single DI container. The same `IActorProvider` registration must serve both.

**SystemActor:**
- Construct `Actor.Create("system", new HashSet<string>(["reminders:dispatch", "reminders:read", "job-runs:read"]))`. Actor has no IsAuthenticated member.

**Composition pattern.** Register one compatible HTTP actor provider, then select `UseWorkerActor(systemActor)` (or direct `AddTrellisWorkerActor`). The shipped decorator distinguishes by context:

- **HTTP-request scope:** `IHttpContextAccessor.HttpContext` is non-null. Derive the actor from `HttpContext.User` per the standard Trellis HTTP pattern. If the request has no usable authenticated identity, return `Maybe<Actor>.None` — the mediator pipeline will map this to `Error.AuthenticationRequired` / HTTP 401.
- **Worker tick scope:** `IHttpContextAccessor.HttpContext` is null (the worker opens its own DI scope on each tick; no HTTP request exists). Return `Maybe.From(SystemActor)`.

Do not write a context-branching provider or register a competing global system provider. The wrapper requires exactly one compatible unkeyed inner provider and delegates HTTP identity/absence unchanged; keyed providers are not competing defaults.

**Endpoint authorisation:**
- `/health` — anonymous.
- `/admin/job-runs/{id}` — requires `job-runs:read`. An unauthenticated request must produce `401 Error.AuthenticationRequired`. A request from an authenticated actor lacking `job-runs:read` must produce `403 Error.Forbidden`. Neither must accidentally resolve to `SystemActor`.

**The point.** This section is the lab's hard test of whether `IActorProvider` composes cleanly across HTTP and non-HTTP contexts in one process. A correct implementation observes both behaviours with a single registration. An incorrect implementation either (a) throws from the worker because `HttpContext` is null, or (b) accidentally grants HTTP requests the system actor's permissions.

## 11. Persistence

- **Database:** SQLite (file-based, zero setup).
- **Connection string** in `appsettings.Development.json`: `Data Source=Reminders.db`.
- **Entities to persist:** `Subscription`, `DispatchAttempt`, `JobRun`.
- **Unique constraints:**
  - `DispatchAttempts(SubscriptionId, Tier, Channel)` — composite unique index (enforces idempotency at the storage layer).
  - `Subscriptions(SubscriberId, PlanId)` — composite unique index (one subscription per subscriber-plan pair).
- **Indexes:**
  - `Subscriptions(IsActive, RenewsAt)` — supports the "due reminders" query.
  - `JobRuns(StartedAt DESC)` — supports "most recent job run" for the health query.
- **Enums** (`NotificationChannel`, `DispatchStatus`, `ReminderTier`, `JobRunOutcome`) stored as strings (use `RequiredEnum<T>` per the Trellis pattern).
- **`Maybe<T>` columns** (`SubscriberPhone`, `CompletedAt`, `ProviderMessageId`, `FailureReason`, `FailureSummary`) stored as nullable columns, round-tripping to `Maybe.None` when null.
- **Database creation:** Use `EnsureCreated()` on startup in development mode. Do NOT use EF Core migrations.

**Idempotency mechanism.** The ACL adapter acquires new attempts with clean-context `TryInsertUniqueAsync`; only `FaultCodes.DuplicateKey` means DuplicateSkip. Load explicit retries by ID. Keep claims separate from staged tick changes and use `FailAfterCommit` for durable failed outcomes (§6.1); no custom provider-exception classifier.

## 12. Error Behavior

This section enumerates worker-layer failure categories. Because the worker has no HTTP response to write, there is no HTTP-status column — only behaviour.

| Situation | Category | Worker behaviour |
|-----------|----------|------------------|
| Gateway error classified Transient (§7) AND `RetryCount < MaxRetries - 1` (= < 4) | Transient | Persist `DispatchAttempt` as `SoftFailed`; increments `JobRun.SoftFailedCount`. Retry on next tick. |
| Gateway error classified Transient (§7) AND `RetryCount >= MaxRetries - 1` (= >= 4, i.e., this would be the 5th attempt) | Retries-exhausted | Persist `DispatchAttempt` as `HardFailed` with reason `"transient retries exhausted"`; increments `JobRun.HardFailedCount`. |
| Gateway error classified Permanent (§7) | Permanent | Persist `DispatchAttempt` as `HardFailed`; increments `JobRun.HardFailedCount`. |
| Gateway returns `Error.AuthenticationRequired` | Fail-fast | Mark `JobRun.Outcome = Failed`, set `FailureSummary`. Halt tick. No further items dispatched. |
| Subscription has `Channel = Sms` but `SubscriberPhone = None` | DataError | Persist `DispatchAttempt` as `HardFailed` with reason `"no phone on file"`; increments `JobRun.HardFailedCount`. Gateway is **not** called. |
| Subscription `IsActive = false` (or missing) between query and dispatch | NoLongerEligible | No `DispatchAttempt` created or mutated; increments `JobRun.SkippedInactiveCount`. Log at Information. |
| Composite unique violation on `DispatchAttempt` insert | DuplicateSkip | Increments `JobRun.SkippedDuplicateCount`. No error. |
| Tick wall-clock budget exceeded mid-batch | TimeBudget | Stop dispatching. Increment `SkippedBudgetCount` by the unattempted count; finalise via `Complete` using §3.3. Remaining work gives `PartiallyFailed` unless the attempted prefix had failures and zero dispatches, which gives `Failed`. |
| Worker stop token signalled | Cancellation | Stop dispatching. Count unattempted items in `SkippedBudgetCount` and finalise with the bounded cleanup boundary. Derive the outcome via §3.3; cancellation does not override an all-failed attempted prefix or turn an already completed successful batch into a failure. |

For the admin HTTP endpoint, standard mappings apply: 401 unauthenticated, 403 missing permission, 404 not found, 422 validation, 400 framework-level. These follow the same conventions as the OM spec.

## 13. Testing Requirements

> **Coverage bar:** the prose below summarises the test categories. The full per-row coverage matrix lives in [`coverage-checklist-subscription-reminder.md`](./coverage-checklist-subscription-reminder.md) and is the binding stop-criterion: every row in §1–§10 of that checklist must have at least one matching assertion.

### 13.1 Domain Tests

Unit tests for value objects and aggregate rules. No external dependencies.

- Value objects: `PlanName`, `ProviderMessageId`, `FailureReason`, every identity type. Each tests `TryCreate` happy path, boundary conditions, null/empty, format violations, equality.
- `Subscription`: valid creation, `RenewsAt > StartedAt` rule, `MarkInactive` idempotency.
- `DispatchAttempt`: every transition (`Pending → Dispatched`, `Pending → SoftFailed`, `Pending → HardFailed`, `SoftFailed → Pending`); every invalid transition rejected; retry-cap escalation (`Pending → HardFailed` when `RetryCount` would exceed `MaxRetries - 1`); domain events raised.
- `JobRun`: counter increments; `Complete` derives `Outcome` correctly from each counter combination; `FailFast` sets `FailureSummary` and `Outcome = Failed`.

### 13.2 Application Tests

Handler tests with fake gateways and fake repositories.

- `RunJobTickCommand`: orchestrates correctly with mixed-outcome batch (some dispatched, some soft-failed, some hard-failed, some skipped); honours wall-clock budget; halts on fail-fast.
- `DispatchReminderCommand`: each branch (success, transient → SoftFailed, transient at retry cap → HardFailed, permanent → HardFailed, authentication-required → fail-fast, duplicate-insert, no-longer-eligible subscription, SMS preference with no phone, retry path with `ExistingAttemptId = Some(...)`).
- Drive every §7 gateway outcome through shipped `Classify()` and verify resulting state/counters; test normalized transient network errors separately from Permanent opaque TransportFault.
- `QueryDueRemindersQuery`: returns only items within the reminder window; respects `maxBatchSize`; excludes inactive subscriptions; excludes triples with `Dispatched` or `HardFailed` attempts; populates `ExistingAttemptId` correctly for `SoftFailed` triples.
- Authorisation: every command/query succeeds with `SystemActor` (or `job-runs:read` actor for §6.5); fails with an actor lacking the required permission.
- Time control: handlers must use the injected `TimeProvider` — tests fix `now` via `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`).

### 13.3 Worker Integration Tests

These tests **must exercise the worker without `WebApplicationFactory`**, because the unit under test is `BackgroundService.ExecuteAsync` (or the orchestrator it delegates to), not an HTTP pipeline. Two patterns are acceptable:

1. **Full hosted-service test.** Use `WorkerHarness<TWorker>.CreateAsync` from `Trellis.Testing.Worker`, with production Mediator/event registrations, SQLite and fake gateways. The harness registers the worker; do not double-register it. Set its SystemActor permissions explicitly.
2. **Direct dispatch test.** Resolve `IMediator` from a test scope and send `RunJobTickCommand` through the registered production pipeline, awaiting completion. Simpler and faster; use for most orchestration scenarios. Use pattern 1 when the assertion specifically targets `BackgroundService` lifecycle (start / stop / cancellation).

Either pattern must:
- Register `TimeProvider` (not `DateTimeOffset.UtcNow`) — use `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing`.
- Register fake `IEmailGateway` and `ISmsGateway` with deterministic outcome injection per `(SubscriptionId, Tier)`.
- Use a real SQLite database (file or shared in-memory connection) — not in-memory EF Core provider — so unique-constraint behaviour is exercised.
- The harness supplies TestActorProvider; do not mistake it for proof of §10. Separate real HTTP-host tests exercise UseWorkerActor and both authenticated and anonymous requests.
- Register the first TimeProvider delay before signaling ready. Await readiness, advance `harness.Time`, then use event/tick completion barriers. Named waits use `LastTickIndexOf(name)` as their global cursor, not `TickCountOf(name)`. No sleeps or duplicate tick execution.

Scenarios to cover:
- One tick with all-success batch → `JobRun.Outcome = Succeeded`, gateway calls recorded.
- One tick with mixed outcomes → counter invariant holds (§3.3).
- Transient → next tick retries → eventual `Dispatched`. Verify the same `DispatchAttempt` row is updated (not a second row inserted).
- Transient × 5 → `HardFailed` after retry cap with reason `"transient retries exhausted"`.
- AuthenticationRequired halts the tick; persist failed JobRun and the attempt as SoftFailed below cap or terminal HardFailed at cap, never Pending. Counters include the offending attempt and the unattempted remainder.
- AuthenticationRequired recovery below the cap: restore healthy gateway, retry the same SoftFailed row via ExistingAttemptId, then Dispatched; no second row.
- Duplicate composite-key insert (simulate by inserting an attempt out-of-band before the tick runs) → `SkippedDuplicateCount` incremented; no exception escapes.
- SMS preference with no phone → `HardFailed` with reason `"no phone on file"`; gateway **not** called (assert fake recorded zero calls for that subscription).

### 13.3.1 Domain Event Pipeline

Use shipped domain-event dispatch with commit ownership preserved. Successful outer ticks publish after the owning commit. `FailAfterCommit` remains a failed result, so ordinary in-process event dispatch is suppressed even after its durable save; required failure-path delivery needs an outbox or explicit post-commit follow-up. The harness does not register dispatch on the application's behalf.

For a successful owning tick, assert N dispatch events after N successful reminders and none before the commit. For fail-fast, separately assert saved state and required outbox/follow-up delivery; do not wait forever for an ordinary failed-result event that is intentionally suppressed.

### 13.4 HTTP Integration Tests

For the two HTTP endpoints, use `WebApplicationFactory` as normal.

- `GET /health` → 200 with expected shape; covers never-run, succeeded, partially-failed, failed last-tick states.
- `GET /admin/job-runs/{id}` → 200 happy path; 401 unauthenticated; 403 authenticated but missing `job-runs:read`; 404 unknown id; 400 missing api-version.
- ProblemDetails: Result mapping plus enrichment produces RFC 9457 `application/problem+json`; no `[Produces("application/json")]` override.
- **Auth composition (per §10).** Send an unauthenticated request to `/admin/job-runs/{id}` and assert **401**, not 200. This guards against the failure mode where the worker's `IActorProvider` accidentally grants `SystemActor` to anonymous HTTP requests.

### 13.5 Observability Tests

- Metrics: assert `reminders.dispatched.total{channel=email}` increments by 1 after a successful email dispatch; assert `reminders.failed.total{category=transient}` increments on soft failure; assert `reminders.tick.duration` records a value after each tick. Use `MeterListener` to capture.
- Logs: assert one Information-level "tick completed" log per tick; assert per-item logs at appropriate levels (`Debug`/`Warning`/`Error`); assert `JobRunId` is present as a structured property on every per-item entry.

## 14. Out of Scope

The following are explicitly excluded from this lab. They are mentioned to prevent scope creep:

- Subscription creation / cancellation HTTP endpoints. Subscriptions are seeded at startup or via direct SQL for the lab.
- Subscriber preference UI.
- Payment processing.
- Multi-tenancy.
- Notification content templating (use a fixed string per tier).
- Outbound webhook callbacks confirming delivery.
- Distributed worker coordination (leader election, sharded ticks). The lab assumes one instance.
- Push notifications (third channel). Email + SMS only.
