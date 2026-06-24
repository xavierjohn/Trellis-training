# Benchmark spec — Submit Order

> **This is a neutral business spec.** It names no framework, no library, no language idiom.
> Implement it in plain ASP.NET Core + EF Core and on Trellis, then score every implementation with
> the same black-box probe suite ([`probes/`](probes/)). The point is **not** "can the code be
> written"; it is **"which correctness defects survive, and what does avoiding them cost with and
> without the framework."**

The operation is **Submit Order** — a focused slice that bundles five independent failure modes into
one endpoint, which is exactly why it is a good measuring stick.

---

## 1. Domain

A tiny inventory/order domain. Three concepts:

- **Product** — something with a finite `stock` that orders draw down.
- **Order** — a customer's basket. Starts as a **draft**; **submitting** it reserves stock for every
  line and moves it to **submitted**. Has a lifecycle: `Draft → Submitted`, and `Draft → Cancelled`.
- **Line item** — one `(product, quantity)` pair on an order.

### Invariants the implementation must uphold

- A product's `stock` is a count of whole units and **must never go negative**.
- A line item's `quantity` is **≥ 1**.
- An order is **submitted at most once**; only a **draft** order may be submitted.
- Submitting reserves stock for **every** line item **atomically** — either all reservations apply
  or none do.
- Submitting requires the caller to hold the **`orders:submit`** permission.

---

## 2. The operation — Submit Order

`Submit` turns a `Draft` order into `Submitted`:

1. The caller must hold the **`orders:submit`** permission. Otherwise the request is **forbidden**.
2. The order must exist and be in **`Draft`**. A missing order is **not found**; a non-draft order
   (already `Submitted` or `Cancelled`) is a **conflict** — it is **not** re-submitted.
3. The order must have **at least one** line item.
4. For **every** line item, the product must have **enough stock** (`stock ≥ quantity`). If **any**
   line item is short, the whole submit **fails** and **no** stock is reserved.
5. On success, **reserve** stock — decrement each product's `stock` by the line quantity — set the
   order's status to `Submitted`, stamp `submittedAt` (UTC), and persist.
6. **Concurrency:** two submits that draw on the same product must not oversell it. If two requests
   race, **at most** `stock` units total may be reserved; the loser is rejected (and may retry).

Insufficient stock, an empty order, and an invalid state transition are **client errors (4xx)** —
never a server fault (5xx) — and the error body must **not** leak internal exception text.

---

## 3. HTTP contract

All bodies are JSON. IDs are GUIDs. The probe suite and both arms depend on **exactly** this
contract, so implement the routes, shapes, and status codes as written.

### Auth

The submit endpoint is permission-gated. The caller presents an actor via the **`X-Actor`** request
header, whose value is a JSON object:

```
X-Actor: {"id":"user-1","permissions":["orders:submit"]}
```

An absent header, malformed header, or one whose `permissions` does **not** include `orders:submit`
means the caller is **not** authorized to submit. The other endpoints are open (no auth) — they
exist only to arrange test state.

### Endpoints

| # | Method & path | Body | Success | Errors |
|---|---|---|---|---|
| 1 | `POST /products` | `{ "name": string, "stock": int >= 0, "price": number >= 0 }` | `201` `{ "id", "name", "stock", "price" }` | `422` for `stock < 0`, `price < 0`, or blank `name` |
| 2 | `GET /products/{id}` | — | `200` `{ "id", "name", "stock", "price" }` | `404` if absent |
| 3 | `POST /orders` | `{ "customerId": guid, "items": [ { "productId": guid, "quantity": int >= 1 } ] }` | `201` `{ "id", "status": "Draft", "customerId", "items": [ { "productId", "quantity" } ] }` | `422` for no items, `quantity < 1`, or a `productId` that does not exist |
| 4 | `GET /orders/{id}` | — | `200` `{ "id", "status", "submittedAt", "customerId", "items": [...] }` | `404` if absent |
| 5 | `POST /orders/{id}/submit` | *(empty)* — `X-Actor` header required | `200` `{ "id", "status": "Submitted", "submittedAt" }` | `403` missing `orders:submit`; `404` no such order; `409` order not in `Draft`; `422` insufficient stock or empty order |

### Status-code rules (these are scored)

- A **business rejection** (insufficient stock, empty order, invalid transition) is **`409`** (state)
  or **`422`** (content) — **never `500`**.
- The error body is **RFC 9457 ProblemDetails** (or at minimum a JSON object with a `detail`/`title`)
  and must **not** contain a stack trace or raw exception message.
- A genuine bug in the service (null-ref, etc.) may surface as `500`; the benchmark treats any `5xx`
  on a *business* path as a defect.

---

## 4. Correctness requirements (what the probes verify)

These are framework-neutral outcomes. Every arm is scored against **the same** probes; each row names
the defect class it guards against.

| ID | Requirement | How it is probed (black-box) | Defect class |
|----|-------------|------------------------------|--------------|
| **R1** | **No oversell under concurrency.** Concurrent submits drawing on the same product never reserve more than its stock. *(Stress probe — relies on real request overlap; a pass is strong evidence of concurrency control, a fail is conclusive.)* | Seed product `stock = 1`. Create N draft orders each reserving 1 unit. Fire all submits concurrently. Assert **exactly 1** succeeds and the product's final `stock` is **0** (never negative). | Oversell / lost update |
| **R2** | **All-or-nothing reservation.** If the order's total demand exceeds stock, no stock is reserved at all. | Seed product `stock = 5`. Create a draft order with two lines on that product (`x3` and `x3`, total `6 > 5`). Submit (must fail). Assert the product's `stock` is still **5**. | Partial-reservation corruption |
| **R3** | **Business failure is a 4xx, not a 5xx, with no leaked internals.** | Seed product `stock = 1`. Create a draft order `[product x5]`. Submit. Assert status is `409`/`422` (not `5xx`) and the body has no stack trace / "Exception" text. | Business-failure-as-500 |
| **R4** | **State guard.** A non-draft order cannot be submitted (and stock is not reserved twice). | Seed product `stock = 10`. Create a draft order `[product x5]`. Submit (succeeds -> stock 5). Submit **again**. Assert the second call is `409` and the product's `stock` is still **5** (not 0). | Missing state guard -> double reservation |
| **R5** | **Authorization.** Submitting without `orders:submit` is forbidden and reserves nothing. | Seed product `stock = 10`. Create a draft order `[product x5]`. Submit with an `X-Actor` lacking `orders:submit` (and with no header). Assert `403` and the product's `stock` is still **10**. | Missing authorization |
| **R6** | *(latent)* **No invalid persisted state.** Stock never goes negative; quantities are >= 1. | Covered indirectly by R1-R4 (final stock assertions) plus the `422` paths on creation. | Invalid-state representable (`int`/`string`) |

A run's **defect score** is the number of R1-R5 a build **fails**. A clean build scores **0**.

---

## 5. The arms

The same spec, implemented three ways. Each arm is a runnable HTTP service exposing the §3 contract on
a configurable port, with a `GET /health` returning `200`.

- **Vanilla (defective)** — plain ASP.NET Core + EF Core, written the hurried way: one fat submit
  method, read-modify-write on stock, per-line saves, throw-on-business-failure, no state guard, no
  authorization. A hand-written *representative* baseline of common omissions, not a captured AI
  output. See [`arms/vanilla/`](arms/vanilla/).
- **Vanilla (correct)** — plain ASP.NET Core + EF Core, written carefully: an explicit concurrency
  token, a transaction, a two-phase reservation, manual error mapping, a state check, an
  authorization check. No framework. It exists to prove the probes are achievable without Trellis —
  a fair rubric, not a Trellis-only one. See [`arms/vanilla-correct/`](arms/vanilla-correct/).
- **Trellis** — the same contract on Trellis building blocks: value objects for stock/quantity, an
  `Order` aggregate whose ETag is a concurrency token, a state machine, and `Result<T>`. See
  [`arms/trellis/`](arms/trellis/).

What the benchmark examines is the **delta in correctness machinery**. Both correct arms pass every
probe. The defective arm shows what is easy to forget. The Trellis arm removes one class outright —
the aggregate ETag closes R1 *by construction*, with no token to declare or remember to bump — and
routes the rest (two-phase reservation, `Result -> 4xx` mapping, the state machine) through primitives
that make the correct shape the path of least resistance. R5 (authorization) is an explicit check in
the minimal Trellis arm too, so it is not a structural difference here. Whether an AI, regenerating
each arm from this spec, drops those guarantees more often without the framework is the
generation-level question the harness is built to measure — see [`README.md`](README.md).

---

## 6. Running the benchmark

See [`README.md`](README.md) for the full procedure. In short: start an arm, then run the probe
suite against its base URL:

```
# start an arm (each arm documents its own run command on a chosen port)
# then, from probes/:
dotnet run --project SubmitOrder.Probes -- --url http://localhost:5080
```

The probe runner prints a per-requirement PASS/FAIL scorecard and exits non-zero if any defect is
present. Record results in [`RESULTS.md`](RESULTS.md).
