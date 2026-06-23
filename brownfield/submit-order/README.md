# Brownfield proof — Submit Order

> Take one messy endpoint, convert one slice, and measure what changes. This is the
> "brutal brownfield" half of Trellis's *proof over surface area*: not "Trellis has more
> features," but "the same operation, written the way a hurried team ships it, carries
> defects that the Trellis version makes **structurally impossible**."

This folder holds a **runnable** before/after for a single operation — **Submit Order** — from
the [Order Management spec](../../specs/order-management.md) (§6.7 and the `Draft → Submitted`
transition in §4):

- **`legacy/`** — a real, compiling ASP.NET Core + EF Core service implementing Submit Order the
  way it's often written under deadline: one fat method, raw primitives, a magic-string status
  check, exceptions for business failures, a per-item save, and a read-modify-write on stock with
  no concurrency control. It ships with **xUnit tests that pass *by reproducing* each defect.**
- **The Trellis "after"** is the existing, independently-scored reference implementation at
  [`after/OrderManagement/`](../../after/OrderManagement/) — *not* a strawman written to win this
  comparison. The relevant code is `Domain/src/Aggregates/Order.cs` (`Submit`),
  `Domain/src/Aggregates/Product.cs` (`ReserveStock`), and
  `Application/src/Orders/SubmitOrderCommand.cs`.

## Run it

```bash
cd legacy
dotnet test                  # 5/5 green = all five legacy defects reproduce
dotnet run --project src     # boots the legacy service (Submit Order endpoint)
```

The Trellis side already builds and passes in [`after/OrderManagement/`](../../after/OrderManagement/).

## The slice

Submit Order turns a `Draft` order into `Submitted`. The spec requires: at least one line item;
sufficient stock for **every** line item; reserve that stock; stamp `SubmittedAt`; emit a domain
event; and gate the whole thing behind the `orders:submit` permission. Insufficient stock or an
invalid transition is a **client** error (4xx), not a server fault.

That one operation bundles five distinct failure modes — which is exactly why it makes a good
proof.

## The five defects (each proven by a passing test)

Every row below is a real test in
[`legacy/tests/SubmitOrderBugTests.cs`](legacy/tests/SubmitOrderBugTests.cs) that **passes because
the bug fires.**

| # | Defect (test) | What the legacy code does | Trellis makes it impossible by… |
|---|---|---|---|
| 1 | **Oversell / lost update** (`Bug1_NoOptimisticConcurrency_ConcurrentSubmits_Oversell`) | `product.Stock -= qty` is a read-modify-write with no concurrency token; the test reproduces the interleave two concurrent requests produce — both read 5, both reserve 5, both commit with no conflict raised → 10 sold from 5 | the aggregate carries an **ETag concurrency token**; the second `SaveChanges` is rejected as a conflict and the request must retry against fresh stock |
| 2 | **Partial-reservation corruption** (`Bug2_PerItemSave_LeavesStockReservedForAnOrderThatNeverSubmits`) | the loop **saves per item**, so when a later line item has no stock, the earlier items are already persisted-decremented — stock reserved for an order still stuck in `Draft` | `Order.Submit` **pre-flights every reservation before mutating any product** ("no partial reservations leak through" — `Order.cs:129‑145`) |
| 3 | **Business failure → HTTP 500 + leaked detail** (`Bug3_InsufficientStock_Returns500WithLeakedMessage_NotA4xx`) | insufficient stock is `throw`-n and the endpoint's catch-all returns `500` with the internal message | `Error.InvalidInput.ForRule(...)` maps to a **4xx client error** + RFC 9457 ProblemDetails, no exception or stack trace (Trellis's default for `InvalidInput` is 422; the OM spec's error table lists 400 — either way a client error, never a 500) |
| 4 | **No state guard → double reservation** (`Bug4_NoStateGuard_ResubmittingReservesStockTwice`) | the only guard is `Status == "Cancelled"`, so re-submitting a `Submitted` order reserves its stock **again** (a 5-unit order consumes 10) | the `LazyStateMachine` permits `Submit` **only from `Draft`**; a second call returns 422 before any stock moves (`Order.cs:147`, `227‑246`) |
| 5 | **Missing authorization** (`Bug5_NoAuthorizationCheck_ActorWithoutPermission_StillSubmits`) | the actor header is parsed but `orders:submit` is never checked — anyone submits | `SubmitOrderCommand : IAuthorize` with `RequiredPermissions = [orders:submit]` is enforced by the mediator pipeline → 403 (`SubmitOrderCommand.cs:14‑18`) |

A sixth issue is latent rather than separately tested: the legacy model uses raw `int Stock` /
`int Quantity` / `string Status`, so **invalid states are representable** (negative stock,
out-of-range quantity, a typo'd status). Trellis's `StockQuantity`, `LineItemQuantity`, and
`OrderStatus` value objects make those persisted states unrepresentable at the type level.

## Why the "after" can't carry these bugs

The defects aren't fixed by being careful — they're absent because the structure doesn't allow
them:

- **`Result` values, not exceptions.** The handler returns `Result<T>` *values*
  (`SubmitOrderCommand.cs:41‑53`) — the not-found is an explicit `Result.Fail`, and `Order.Submit`
  returns a `Result` — so business failures are values, not exceptions, and the "collapse to 500"
  catch-all (defect 3) has nothing to catch.
- **Two-phase reservation.** `Order.Submit` validates every line item against stock before it
  mutates anything, so a later shortfall can't corrupt earlier items (defect 2).
- **A real state machine, not a magic string.** `Draft → Submitted` is the only permitted
  transition; re-submitting is rejected (defect 4) and invalid transitions return a 4xx.
- **Declarative authorization.** `IAuthorize` is checked by the pipeline before the handler runs,
  so it can't be forgotten (defect 5).
- **Aggregate concurrency.** Trellis aggregates carry an ETag concurrency token and commit through
  the Unit-of-Work, so a stale write is rejected on commit — closing the lost-update window (defect 1).
- **Value objects.** Stock and quantity are typed (`StockQuantity`, `LineItemQuantity`); the
  persisted domain can't hold a negative stock or an out-of-range quantity (defect 6).

## The numbers

Raw line count is *not* the headline — the Trellis version is deliberately spread across value
objects, an aggregate, and a handler, and that distribution is the point: each piece is small and
independently verifiable. What shrinks is the **review surface and the defect count.**

| Dimension (Submit Order slice) | Legacy (before) | Trellis (after) |
|---|---|---|
| Demonstrated defects | **5** (+ 1 latent invalid-state) | **0** |
| Business rules expressed as thrown exceptions | 4 throw sites (2 → 500 via the catch-all, 2 → 404) | 0 |
| Catch-all that maps business rules → 500 + leaked message | 1 | 0 |
| Optimistic-concurrency protection | none | aggregate ETag concurrency token |
| Reservation atomicity | per-item save (partial corruption) | two-phase pre-flight |
| State guard | one magic-string `if` | `LazyStateMachine`, transitions enumerated |
| Authorization | none | `IAuthorize [orders:submit]` |
| Invalid domain states representable | yes (`int` / `string`) | no (value objects) |
| Where the core rule is testable | EF/service-level (needs a `DbContext`) | **pure domain unit test** (`Order.Submit`, no infra) |

The last row is the quiet one that matters most. In the legacy version the only place the
"reserve all-or-nothing" rule *exists* is tangled inside an `async` method that loads from and
writes to the database, so testing it requires a `DbContext` (an EF/service-level test, as in
`legacy/tests/`). In the Trellis version the rule lives in `Order.Submit(products, time)` — a pure
method you can exercise with a dictionary and zero infrastructure. Correctness becomes a fast unit
test instead of a slow, stateful one.

## Honesty notes

- The **before** is not a strawman: every defect here (per-item save, throw-for-business-failure,
  read-modify-write stock, forgotten permission check, magic-string status) is a pattern teams ship
  to production. It is, if anything, *tidier* than most real legacy.
- The **after** is the unmodified reference implementation already scored in this lab, so the
  comparison isn't rigged by hand-tuning the winner.
- This is one slice. The claim is narrow and concrete: *for this operation*, conversion removes
  five reproducible defects and moves the core rule from integration-only to unit-testable. That is
  the brownfield proof — repeatable on the next slice.
