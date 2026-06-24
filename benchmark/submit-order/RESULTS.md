# Results — Submit-Order benchmark

A recorded run of [`run.ps1`](run.ps1): three implementations of the same [`SPEC.md`](SPEC.md), scored
by the same [probe suite](probes/). Re-run it yourself to reproduce.

## Scorecard

```
================= SCORECARD =================
  vanilla          5 defect(s) of 5
  vanilla-correct  0 defect(s) of 5
  trellis          0 defect(s) of 5
============================================
```

| Requirement | `vanilla` (defective) | `vanilla-correct` | `trellis` |
|-------------|:--------------------:|:-----------------:|:---------:|
| R1 — No oversell under concurrency | ❌ | ✅ | ✅ |
| R2 — All-or-nothing reservation | ❌ | ✅ | ✅ |
| R3 — Business failure is 4xx not 5xx | ❌ | ✅ | ✅ |
| R4 — State guard (no double submit) | ❌ | ✅ | ✅ |
| R5 — Authorization required | ❌ | ✅ | ✅ |
| **Total defects** | **5** | **0** | **0** |

Two things to read off this table:

1. **The rubric is fair, not Trellis-only.** `vanilla-correct` is plain ASP.NET Core + EF Core with
   no framework, and it scores `0`. A careful developer passes every probe without Trellis — so a
   passing score is not a Trellis trick, and the probes assert genuinely framework-neutral outcomes.
2. **The defects are real and common.** `vanilla` is the same plain stack written the hurried way,
   and it fails all five. These are five independent, ordinary omissions in a single endpoint.

The interesting comparison is therefore **not** "Trellis passes and vanilla fails" — a careful
vanilla passes too. It is **what each correct arm has to get right by hand**, shown below.

## What "correct" costs in each arm

Each row is the requirement, the defect the hurried `vanilla` arm exhibits, what the correct plain
arm (`vanilla-correct`) must do **by hand** to pass, and what the `trellis` arm gets from a building
block instead.

| # | `vanilla` defect (observed) | What `vanilla-correct` hand-rolls | What `trellis` uses instead |
|---|------------------------------|-----------------------------------|------------------------------|
| **R1** | 8 of 8 concurrent single-unit submits succeeded against `stock = 1` — a lost update. | A `Product.Version` column declared `IsConcurrencyToken()`, **manually bumped on every stock change**, plus a `DbUpdateConcurrencyException` catch. Forget the bump and it oversells. | Nothing extra. `Product` derives from `Aggregate`, whose **ETag** the Trellis EF conventions configure as a concurrency token and an interceptor re-stamps on every save. The token is inherent to the aggregate — no separate field to declare or remember to bump. **This is the one requirement Trellis makes structural.** |
| **R2** | A submit that failed on a later line left stock partially drawn down (`stock = 2`, expected `5`). | Aggregate demand per product, validate all reservations, then apply — and rely on EF's single `SaveChanges` transaction so nothing persists on failure. Written by hand in the endpoint. | The same two-phase logic, written by hand in `Order.Submit`. Trellis does not generate it; the aggregate boundary and `Result<T>` (commit only on success) are what keep it in one place. |
| **R3** | An insufficient-stock submit returned `500` with an internal message. | Return `Results.Problem(..., 422)` for the business case instead of throwing — i.e. remember not to let a business failure become an exception. | The domain returns business failures as `Result<Error>` **values** (`Error.InvalidInput`), so "throw → 500" is not the default path. The endpoint maps `Error` → status (by hand here; automatically via `Trellis.Asp` in a fuller app). |
| **R4** | Re-submitting an already-submitted order returned `200` and reserved stock again. | A hand-written `if (order.Status != "Draft") return 409;` guard. | A **state machine** that permits `Submit` only from `Draft`; the transition itself rejects a re-submit, with an explicit `Error.Conflict` chosen to surface it as `409`. |
| **R5** | Submitting without the `orders:submit` permission returned `200`. | A hand-written permission check on the actor header. | A permission check, also explicit in this minimal arm. *(Trellis offers declarative `IAuthorize` — see [`after/OrderManagement`](../../after/OrderManagement/Application/src/Orders/SubmitOrderCommand.cs) — but this arm deliberately stays minimal and checks by hand, so R5 is **not** a structural difference here.)* |

## Interpretation

- **Correctness is achievable either way.** Both correct arms pass. Trellis is not required to write a
  correct submit endpoint.
- **The difference is the surface you must get right.** `vanilla-correct` carries an explicit
  concurrency token (and the discipline to bump it), a hand-written two-phase reservation, manual
  status mapping, a manual state guard, and a manual authorization check — five separate things to
  remember, and the hurried `vanilla` arm forgot all five. The `trellis` arm removes one class
  outright (R1 is inherent to the aggregate) and routes the rest through primitives — value objects,
  a state machine, `Result<T>` — that make the correct shape the path of least resistance.
- **Honest scope of the structural claim.** Only **R1** is "you cannot forget it" in the Trellis arm.
  R2–R5 still require correct domain and endpoint code; the building blocks shrink and guide that
  code, they do not write it.

## From this floor to the generation claim

This recorded run is an existence proof: the defects are real, a careful non-Trellis service avoids
them, and Trellis removes at least one class by construction. It does **not**, on its own, measure the
headline claim that *an AI generates more correct code on Trellis*.

That claim is measured by using this harness as the instrument: regenerate the `vanilla` and `trellis`
arms N times from `SPEC.md` with the same model and prompt, run every generation through the probes,
and compare the defect distributions. The hypothesis to test is that the five things `vanilla-correct`
had to remember are exactly what an AI drops intermittently, while the Trellis primitives hold them in
place. See [`README.md`](README.md#extending-it-to-a-generation-study).

## Caveats

- **`vanilla` is a hand-written representative baseline, not a captured AI output.** It was written to
  embody five *common* omissions so the probes have something to detect and the taxonomy is concrete.
  It is deliberately defective; it is not evidence about what any particular model emits. The
  generation study above is what would turn it into that evidence.
- **R1 is a concurrency stress probe, not a deterministic proof.** It fires several concurrent submits
  and asserts exactly one wins. If a server happened to serialize every request it could pass without
  real concurrency control, so a pass is strong evidence, not a guarantee; a fail (as `vanilla`
  produces) is conclusive. The other probes are deterministic.
- **A single run is an existence proof, not a statistic.** The durable value is the mechanism: the
  `vanilla` defects trace to ordinary omissions, and the correct arms show the cost of avoiding them
  with and without the framework.
