# Trellis vs. non-Trellis — the Submit-Order benchmark

A controlled, repeatable way to examine one question:

> **When the same feature is built with and without Trellis, which correctness defects survive — and
> what does avoiding them cost in each case?**

The same neutral business spec ([`SPEC.md`](SPEC.md)) is implemented three ways and scored by the
**same** black-box probe suite. The probes describe *outcomes a correct service must exhibit*, not
Trellis features, so no arm is judged on a home-field rubric.

## The three arms

| Arm | What it is | Expected score |
|-----|------------|:--------------:|
| [`arms/vanilla/`](arms/vanilla/) | Plain ASP.NET Core + EF Core, written the **hurried** way: one fat submit method, read-modify-write on stock, per-line saves, throw-on-business-failure, no state guard, no authorization. | 5 / 5 defects |
| [`arms/vanilla-correct/`](arms/vanilla-correct/) | Plain ASP.NET Core + EF Core, written **carefully**: an explicit concurrency token, a transaction, a two-phase reservation, manual error mapping, a state check, an authorization check. No framework. | 0 defects |
| [`arms/trellis/`](arms/trellis/) | The same contract on **Trellis** building blocks: value objects, an aggregate with an ETag concurrency token, a state machine, `Result<T>`. | 0 defects |

`vanilla-correct` is the load-bearing arm for fairness: because a careful non-Trellis service scores
`0`, a passing score is **not** a Trellis-only trick, and the rubric is demonstrably achievable
without the framework. The defective `vanilla` arm makes the failure modes concrete. The `trellis`
arm shows the same correctness reached through primitives instead of hand-rolled machinery.

## What the benchmark does and does not claim

- **It shows** that the five defects are real, that a careful plain implementation avoids them, and
  exactly **what that careful implementation has to get right by hand** versus what Trellis supplies
  as a building block. See [`RESULTS.md`](RESULTS.md).
- **It does not, on its own, prove** the headline claim that *an AI generates more correct code on
  Trellis*. The `vanilla` arm is a hand-written representative baseline, not a captured AI output. The
  harness is built to *measure* that claim — see [Extending it to a generation study](#extending-it-to-a-generation-study) — but a single recorded run is an existence proof, not a statistic.

## Layout

| Path | What it is |
|------|------------|
| [`SPEC.md`](SPEC.md) | The neutral business spec + HTTP contract + the R1–R5 correctness requirements. |
| [`probes/`](probes/) | The executable rubric: a `--url`-parametric black-box probe runner. Exits non-zero with a per-requirement scorecard. |
| [`arms/`](arms/) | The three implementations (`vanilla`, `vanilla-correct`, `trellis`). |
| [`run.ps1`](run.ps1) | Builds everything, runs each arm through the probes, prints a side-by-side scorecard. |
| [`RESULTS.md`](RESULTS.md) | A recorded run and the per-requirement "what correct costs" comparison. |

## Running it

Prerequisites: the .NET 10 SDK, and network access to restore the Trellis packages (for the `trellis`
arm) from NuGet.org.

```pwsh
# from this directory — runs all three arms and prints a scorecard
./run.ps1

# or a subset
./run.ps1 -Arms vanilla,trellis
```

Or drive one arm by hand:

```pwsh
# terminal 1 — start an arm on a port
cd arms/vanilla-correct          # or arms/vanilla, arms/trellis
$env:ASPNETCORE_URLS = 'http://localhost:5080'
dotnet run -c Release

# terminal 2 — probe it
cd probes
dotnet run -c Release -- --url http://localhost:5080
```

The probe runner prints `PASS`/`FAIL` per requirement and exits with the number of defects (`0` =
clean), so it can gate CI.

## How to read a result

Each probe (R1–R5) maps to one defect the
[SPEC](SPEC.md#4-correctness-requirements-what-the-probes-verify) defines; a `FAIL` means the arm
exhibits it. The expectation is `vanilla` 5, `vanilla-correct` 0, `trellis` 0. The
[results writeup](RESULTS.md#what-correct-costs-in-each-arm) lines up, per requirement, the defect, the
hand-rolled machinery the correct plain arm needs, and the Trellis building block that replaces it.
Only **R1** (concurrency) is genuinely "impossible to forget" in the Trellis arm — that honesty is
spelled out in the results.

## Extending it to a generation study

The single-shot result here is the *floor*. To measure the AI-generation claim directly:

1. Regenerate the `vanilla` and `trellis` arms N times from `SPEC.md` with the same model and prompt,
   capturing each raw generation (no hand-editing, no post-hoc bug seeding).
2. Run every generation through the probe suite.
3. Chart the defect distribution per arm.

The hypothesis to test is that the five things `vanilla-correct` had to remember are exactly what an
AI drops intermittently when the framework is absent, while the Trellis primitives hold them in place.
The harness is already the measuring instrument — only the arms get regenerated.

## Honesty notes

- **The `vanilla` arm is a hand-written representative baseline, not a strawman and not an AI output.**
  Its submit is the kind of one-method, read-modify-write, throw-on-business-failure code a hurried
  team plausibly writes; each defect is a *common* omission. It is deliberately defective so the
  probes have something concrete to detect — it is not evidence about any specific model.
- **`vanilla-correct` proves the probes are framework-neutral.** It is plain ASP.NET Core + EF Core and
  scores `0`. Anything a correct service must do, it does — without Trellis.
- **The probes are black-box.** They speak only HTTP and assert outcomes any correct implementation
  must satisfy.
- **R1 is a concurrency stress probe.** It relies on real request overlap to expose a lost update; a
  pass is strong evidence of concurrency control, a fail is conclusive. The other probes are
  deterministic.
- **The `trellis` arm is deliberately minimal.** It uses only the building blocks the requirements
  need and checks authorization with an explicit `if` rather than Trellis's declarative `IAuthorize`
  (which a fuller service would use — see [`after/OrderManagement`](../../after/OrderManagement/)), so
  the comparison stays about the core primitives, not a large reference application.
