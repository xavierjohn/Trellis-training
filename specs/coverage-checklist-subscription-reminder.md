# Test Coverage Checklist — Subscription Renewal Reminder Worker (v1)

Companion to `specs/subscription-reminder-worker.md`. This checklist makes the expected test coverage explicit and machine-checkable so models stop at "rubric coverage" rather than "representative happy + key failure paths."

Each row should be a separate test (or a single parameterised test with named cases). A row is **green** when the implementation has at least the listed **positive** and **negative** assertions.

Where it differs from `coverage-checklist.md` (the OM checklist): there are dedicated sections for the background-worker tick (§4), external-gateway error mapping (§5), idempotency (§6), and observability (§8). The OM HTTP-endpoint section (§5 in that file) shrinks here to two endpoints (§7).

## Required eval minimum (subset)

The rubric L4 (`docs/evaluation-criteria.md` Level 4) actually scores against this minimum subset. Rows outside the minimum are extended completeness — required for "test-complete" but not individually scored.

| § | Minimum row |
|---|---|
| §1 | Every scalar VO: `TryCreate` happy path + at least one boundary failure + null/empty failure |
| §1 | Reused Trellis built-ins (`EmailAddress`, `PhoneNumber`) integrate cleanly into `Subscription` — invalid input rejected at aggregate construction. (Do not re-test the built-ins' internal pattern rules.) |
| §2 | Every state-machine transition: happy path + side-effect verified + domain event raised |
| §2 | Every invalid transition: `Result.Fail` with `Error.InvalidInput`; no state mutation |
| §3 | `Subscription`: `RenewsAt <= StartedAt` rejected; `MarkInactive` idempotent |
| §3 | `JobRun.Complete`: each counter combination derives the correct `Outcome` |
| §3 | `JobRun` counter invariant: `Dispatched + SoftFailed + HardFailed + SkippedDuplicate + SkippedInactive == DueCount` on a normally-completed tick |
| §4 | `RunJobTickCommand`: happy-path batch + mixed-outcome batch + wall-clock budget + fail-fast |
| §4 | `DispatchReminderCommand`: every branch in §6.2 of the spec, including the retry path with `ExistingAttemptId = Some(...)` |
| §4 | Domain event pipeline: `ReminderDispatchedDomainEvent` raised in a tick reaches a registered `IDomainEventHandler<T>` in the worker scope |
| §5 | Each Trellis `Error` type listed in spec §7 maps to the correct category (Transient / Permanent / Fail-fast). Hand-rolled parallel Transient/Permanent enums are a deduction. |
| §6 | Composite unique-key violation produces `Skipped(Duplicate)`, not an exception |
| §7 | `GET /health`: happy path + never-run + after-failed-tick |
| §7 | `GET /admin/job-runs/{id}`: 200 + 401 + 403 + 404 + 400 |
| §7 | Auth composition: an unauthenticated request to `/admin/job-runs/{id}` returns 401, not 200 (`SystemActor` is not granted to anonymous HTTP requests) |
| §8 | Per-channel `reminders.dispatched.total` increments verified |
| §8 | `JobRunId` present as a structured log property on every per-item entry |

## Extended completeness

Everything below is required for "test-complete" but not individually scored by L4.

## 1. Scalar value objects (`Domain/tests`)

For every scalar VO declared in the spec — `PlanName`, `ProviderMessageId`, `FailureReason`, and the strongly-typed identity types (`SubscriptionId`, `SubscriberId`, `PlanId`, `DispatchAttemptId`, `JobRunId`):

| Coverage | Required |
|---|---|
| `TryCreate` happy path | ≥1 valid input returns `Result.Ok` and round-trips |
| `TryCreate` boundary low | minimum-length input returns `Result.Ok` |
| `TryCreate` boundary high | maximum-length input returns `Result.Ok` |
| `TryCreate` below low | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` above high | `Result.Fail` with `Error.InvalidInput.ForField(...)` |
| `TryCreate` null/empty/whitespace | `Result.Fail` |
| Format / pattern violation (where applicable — `EmailAddress`, `PhoneNumber`) | `Result.Fail`. (`ProviderMessageId` has no pattern beyond the 1–200 char length bound; no separate format row required.) |
| Equality and `GetHashCode` | two VOs with identical inputs are equal; differing inputs are not equal |

Reused Trellis built-ins (`EmailAddress`, `PhoneNumber`, `Money`) do **not** need re-testing of their internal pattern rules — only that they integrate correctly into `Subscription`. Verify integration by constructing `Subscription` with a bad email and asserting the failure surfaces with `Error.InvalidInput` for the right field; do not re-test RFC 5322 / E.164 conformance per se.

## 2. State machine (`Domain/tests`)

For every transition on `DispatchAttempt` declared in spec §4:

| Coverage | Required |
|---|---|
| `Pending → Dispatched` happy path | status updated; `ProviderMessageId` set; `CompletedAt` set; `ReminderDispatchedDomainEvent` raised |
| `Pending → SoftFailed` happy path | status updated; `FailureReason` set; `RetryCount` incremented; `LastAttemptedAt` updated; `ReminderSoftFailedDomainEvent` raised |
| `Pending → SoftFailed` retry-cap escalation | when `RetryCount` would reach `MaxRetries`, status becomes `HardFailed` instead with reason `"transient retries exhausted"`; `ReminderHardFailedDomainEvent` raised |
| `Pending → HardFailed` permanent | status updated; `FailureReason` set; `CompletedAt` set; `ReminderHardFailedDomainEvent` raised |
| `Pending → HardFailed` data error (SMS-without-phone) | gateway not invoked (verified via fake); status set with reason `"no phone on file"` |
| `SoftFailed → Pending` retry reset | status updated; `LastAttemptedAt` refreshed; no event |
| Invalid transition — `Dispatched → *` | `Result.Fail` with `Error.InvalidInput`; no state mutation |
| Invalid transition — `HardFailed → *` | `Result.Fail` with `Error.InvalidInput`; no state mutation |
| Invalid transition — `SoftFailed → Dispatched` directly | `Result.Fail`; must go via `Pending` |
| Idempotency — `MarkDispatched` called twice | second call `Result.Fail`; `ProviderMessageId` and `CompletedAt` unchanged |

## 3. Aggregate invariants (`Domain/tests`)

| Coverage | Required |
|---|---|
| `Subscription`: `RenewsAt <= StartedAt` | rejected at construction; `Result.Fail` with `Error.InvalidInput` |
| `Subscription`: SMS preference with no phone | construction succeeds (worker handles at dispatch); no domain-level rejection |
| `Subscription.MarkInactive` idempotency | calling twice does not throw and leaves `IsActive = false` |
| `JobRun.Complete` → `Succeeded` | counters all zero failures → `Outcome = Succeeded` |
| `JobRun.Complete` → `PartiallyFailed` | at least one dispatched and at least one failed → `Outcome = PartiallyFailed` |
| `JobRun.Complete` → `Failed` (no successes) | zero dispatched and at least one hard/soft failure → `Outcome = Failed` |
| `JobRun.FailFast` | `Outcome = Failed`, `FailureSummary` set, `CompletedAt` set |
| `JobRun` counter monotonicity | counters never decrement |

## 4. Worker tick orchestration (`Application/tests`)

For `RunJobTickCommand`:

| Coverage | Required |
|---|---|
| Happy path — empty due batch | `JobRun.Outcome = Succeeded`; counters all zero; one tick log emitted |
| Happy path — all dispatched | `JobRun.Outcome = Succeeded`; `DispatchedCount` matches due count |
| Mixed outcomes | counters match: `Dispatched + SoftFailed + HardFailed + SkippedDuplicate + SkippedInactive == DueCount` (per spec §3.3 invariant) |
| Wall-clock budget exceeded mid-batch | dispatch loop stops; `JobRun.Outcome = PartiallyFailed`; remaining items not attempted (verified via fake gateway call count) |
| Fail-fast on `Error.AuthenticationRequired` | tick halts at the offending item; `JobRun.Outcome = Failed`; `FailureSummary` populated; subsequent items not attempted |
| Cancellation token signalled | dispatch loop exits; `JobRun.Outcome = PartiallyFailed`; `CompletedAt` set |
| Per-tick DI scope | each tick resolves handlers from a fresh scope (assert by registering a scoped marker service and verifying a different instance per tick) |
| Time source | `TimeProvider.GetUtcNow()` used to populate `tickStartedAt` (assert by advancing `FakeTimeProvider` and checking persisted `JobRun.StartedAt`) |
| `SystemActor` resolution in tick scope | `IActorProvider.GetCurrentActorAsync()` returns `Maybe.From(SystemActor)` inside the tick (where `HttpContext` is null); permissions include `reminders:dispatch` |
| Domain event pipeline | a test `IDomainEventHandler<ReminderDispatchedDomainEvent>` registered in DI observes one event per successful dispatch; the worker must not bypass `IDomainEventPublisher` / `DomainEventDispatchBehavior` |

For `DispatchReminderCommand`:

| Coverage | Required |
|---|---|
| Happy path — email | gateway called once; `DispatchAttempt` persisted as `Dispatched` with `ProviderMessageId`; outcome `Dispatched` |
| Happy path — SMS | gateway called once; same as above |
| Subscription not found | outcome `Skipped(NoLongerEligible)`; gateway not called |
| Subscription inactive | outcome `Skipped(NoLongerEligible)`; gateway not called |
| SMS preference, no phone | outcome `HardFailed(DataError)`; gateway not called; `DispatchAttempt` persisted as `HardFailed` with reason `"no phone on file"`; `JobRun.HardFailedCount` incremented (not a skip counter) |
| Composite unique-key violation on insert (first attempt, `ExistingAttemptId = None`) | outcome `Skipped(Duplicate)`; no `DispatchAttempt` mutation; no exception escapes |
| Retry path (`ExistingAttemptId = Some(id)`) | attempt loaded by id; `ResetForRetry` called; then proceeds to gateway call; on success transitions `Pending → Dispatched`; the existing row is updated (no second row inserted) |
| Retry path with attempt no longer `SoftFailed` (race) | outcome `Skipped(Duplicate)`; gateway not called |

## 5. Gateway error mapping (`Application/tests`)

For each gateway `Error` listed in spec §7. The implementation must classify using existing Trellis `Error` types — hand-rolled parallel Transient/Permanent enums are not acceptable.

| Coverage | Required |
|---|---|
| Gateway returns `Result.Ok(messageId)` | `DispatchAttempt → Dispatched`; metric `reminders.dispatched.total{channel=*}` incremented |
| Gateway returns `Error.Unavailable(...)` AND retry available (`RetryCount < 4`) | classified Transient; `DispatchAttempt → SoftFailed`; `RetryCount` incremented; metric `reminders.failed.total{category=transient}` incremented |
| Gateway returns `Error.RateLimited(RetryAdvice)` | classified Transient (same behaviour as `Unavailable`) |
| Gateway returns `Error.Unexpected(...)` | classified Transient (same behaviour as `Unavailable`) |
| Gateway returns `Error.TransportFault(...)` | classified Transient (same behaviour as `Unavailable`) |
| Gateway returns Transient `Error` AND retry exhausted (`RetryCount == 4`) | `DispatchAttempt → HardFailed` with reason `"transient retries exhausted"`; metric `reminders.failed.total{category=retries-exhausted}` incremented |
| Gateway returns `Error.InvalidInput(...)` | classified Permanent; `DispatchAttempt → HardFailed`; metric `reminders.failed.total{category=permanent}` incremented |
| Gateway returns `Error.InvariantViolation(...)` | classified Permanent (same behaviour as `InvalidInput`) |
| Gateway returns `Error.Forbidden(...)` | classified Permanent (gateway's credentials valid but recipient refused) |
| Gateway returns `Error.AuthenticationRequired(...)` | orchestrator fail-fast; `JobRun.Outcome = Failed` |
| Gateway throws unexpected exception | exception caught and translated to `Result.Fail(Error.Unexpected(...))` (not allowed to bubble out of the handler) |
| Gateway respects `CancellationToken` | when token is signalled mid-call, handler observes the cancellation cleanly |
| Classification helper location | classification logic is co-located with `DispatchReminderCommand` in a single helper, not duplicated across handlers (verified by code-shape inspection or by parameterised test driving every `Error` type through one entry point) |

## 6. Idempotency (`Application/tests` + `Acl/tests`)

| Coverage | Required |
|---|---|
| Storage-layer unique constraint | duplicate insert of `(SubscriptionId, Tier, Channel)` raises EF Core unique-constraint violation; handler catches and reports `Skipped(Duplicate)` |
| Cross-tick idempotency | tick 1 dispatches; tick 2 queries due reminders and excludes the already-`Dispatched` triple (`ExistingAttemptId = None` would not be produced) |
| Retry idempotency | tick 1 produces `SoftFailed`; tick 2 retries via `ExistingAttemptId = Some(id)`; only one row exists per triple after both ticks (no orphan `SoftFailed` left behind) |
| First-attempt race | two simulated concurrent first-attempt inserts for the same triple → one succeeds; the other catches the unique-constraint violation and reports `Skipped(Duplicate)` |
| Fail-fast preserves prior dispatches in same tick | dispatches before the `AuthenticationRequired` item are persisted and visible after the tick aborts |

## 7. HTTP endpoints (`Api/tests`)

For each of the two HTTP endpoints in spec §8:

| Coverage | Required |
|---|---|
| `GET /health` happy path — after a successful tick | 200 with full counts; `lastTickOutcome = "Succeeded"` |
| `GET /health` never-run | 200 with `lastTickAt: null`, `lastTickOutcome: "never-run"` |
| `GET /health` after partially-failed tick | 200 with `lastTickOutcome = "PartiallyFailed"`; counts reflect failures |
| `GET /health` after failed tick | 200 with `lastTickOutcome = "Failed"`; counts reflect zero dispatched |
| `GET /admin/job-runs/{id}` happy path | 200 with expected body shape per §8 |
| `GET /admin/job-runs/{id}` not found | 404 with ProblemDetails body |
| `GET /admin/job-runs/{id}` unauthenticated | 401 with ProblemDetails body (`Error.AuthenticationRequired`); `SystemActor` is **not** granted to anonymous HTTP requests |
| `GET /admin/job-runs/{id}` authenticated but missing permission | 403 with ProblemDetails body |
| `GET /admin/job-runs/{id}` missing api-version | 400 (framework-level) |
| ProblemDetails wrapping | error responses follow RFC 7807 via `AddTrellisProblemDetails` |
| Auth composition end-to-end | with the worker `IActorProvider` registration in place, the HTTP pipeline still resolves anonymous requests to `Maybe<Actor>.None` and authenticated requests to the bearer-token-derived actor — not `SystemActor` |

## 8. Observability (`Application/tests` + `Api/tests`)

| Coverage | Required |
|---|---|
| `reminders.dispatched.total{channel=email}` increment | captured via `MeterListener` after one email dispatch |
| `reminders.dispatched.total{channel=sms}` increment | captured via `MeterListener` after one SMS dispatch |
| `reminders.failed.total{category=transient}` increment | captured after a soft failure |
| `reminders.failed.total{category=retries-exhausted}` increment | captured when a transient failure escalates to HardFailed at the cap |
| `reminders.failed.total{category=permanent}` increment | captured after a permanent gateway error |
| `reminders.failed.total{category=data-error}` increment | captured after an SMS-without-phone HardFailed |
| `reminders.skipped.total{reason=duplicate}` increment | captured after a duplicate composite-key insert |
| `reminders.skipped.total{reason=inactive}` increment | captured after a NoLongerEligible skip |
| `reminders.tick.duration` recorded | one histogram observation per completed tick |
| `reminders.batch.size` recorded | one histogram observation per completed tick |
| Per-tick log entry | exactly one Information-level "tick completed" entry per tick; contains every count field |
| Per-item log entries — Dispatched | Debug level, structured property `Status = "Dispatched"` |
| Per-item log entries — SoftFailed | Warning level, contains `RetryCount`, `Provider`, `Reason` |
| Per-item log entries — HardFailed | Error level, contains `Reason` |
| Fail-fast log | one Error-level "job run aborted" entry, contains `FailureSummary` |
| `JobRunId` structured property | present on every per-item log entry within a tick (verified via `ILogger.BeginScope` or `LoggerMessage` enrichment) |
| Structured logging hygiene *(recommended, not binding)* | log entries use `{Name}` placeholders rather than string interpolation so fields are emitted as named properties. Verify the `JobRunId`, `Status`, `Reason`, `Provider`, `RetryCount` fields exist as structured properties; do not assert against the message-template string itself. |

## 9. Round-trip persistence (`Acl/tests`)

For every aggregate root and every owned VO:

| Coverage | Required |
|---|---|
| `Subscription` insert + reload | every property (`SubscriberPhone` absent + present, `MonthlyPrice`, `PreferredChannel`, `IsActive`) survives a save + reload |
| `DispatchAttempt` insert + reload | every property (`CompletedAt` absent + present, `ProviderMessageId` absent + present, `FailureReason` absent + present, `RetryCount`) round-trips |
| `JobRun` insert + reload | every counter (`DueCount`, `DispatchedCount`, `SoftFailedCount`, `HardFailedCount`, `SkippedDuplicateCount`, `SkippedInactiveCount`) + `Outcome` + `FailureSummary` (absent + present) round-trips |
| `Maybe<T>` absent | `null` column persists as `Maybe<T>.None`; reloads as `None` |
| `Maybe<T>` present | persists and reloads to a `Some` with equal value |
| `RequiredEnum<T>` (Channel, Status, Tier, Outcome) | persists as the declared field name; reloads to the same enum value |
| Composite unique index on `DispatchAttempts(SubscriptionId, Tier, Channel)` | second insert of same triple fails with EF Core unique-constraint violation |
| Index on `Subscriptions(IsActive, RenewsAt)` | declared in model; verified via `Model` inspection or migration schema dump |

## 10. Specifications and queries (`Acl/tests`)

Per spec §6.3, the "due reminders" logic is split into two pieces, tested separately.

### 10.1 `DueSubscriptionWindowSpecification` — a `Specification<Subscription>` covering subscription-level predicate only.

| Coverage | Required |
|---|---|
| In-memory `IsSatisfiedBy` — active subscription inside the reminder window | returns true |
| In-memory `IsSatisfiedBy` — active subscription outside the window | returns false |
| In-memory `IsSatisfiedBy` — inactive subscription inside the window | returns false |
| EF translation via `Where(spec)` | hits SQLite in an integration test; returns the same row count as in-memory evaluation over the same data |
| `Maybe<T>` member access translates (if the spec touches `SubscriberPhone` or similar) | translatable via `MaybeQueryInterceptor` |

### 10.2 Attempt-state join query — selects the most-recent `DispatchAttempt` per triple and produces the `DueReminder` projection with `ExistingAttemptId`.

| Coverage | Required |
|---|---|
| Triple with no prior attempt | included; `ExistingAttemptId = None` |
| Triple whose most recent attempt is `SoftFailed` | included; `ExistingAttemptId = Some(id)` of the soft-failed attempt |
| Triple whose most recent attempt is `Dispatched` | excluded |
| Triple whose most recent attempt is `HardFailed` | excluded |
| Triple with multiple historical attempts | only the most recent is considered |
| `maxBatchSize` cap | result count `<= maxBatchSize`; ordered by `RenewsAt` ascending |

## 11. Stop criteria

The implementation is "test-complete" when every row in §1–§10 has at least one matching assertion. Stopping earlier (e.g., "representative happy + key failure paths") is not sufficient and is explicitly deprecated for eval runs (see `eval-runs/README.md` § Eval Hygiene if a runs folder exists for this lab).
