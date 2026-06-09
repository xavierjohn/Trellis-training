<p align="center">
  <img src="docs/images/hero-banner.png" alt="Trellis Training Lab — learn to build enterprise .NET services with AI" width="800"/>
</p>

# Trellis Training Lab

[![Build](https://img.shields.io/badge/lab-corpus-blue.svg)](docs/training-lab.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download)
[![C#](https://img.shields.io/badge/C%23-14.0-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![GitHub Stars](https://img.shields.io/github/stars/xavierjohn/Trellis-training?style=social)](https://github.com/xavierjohn/Trellis-training/stargazers)

> **Learn to build enterprise .NET services with the [Trellis](https://github.com/xavierjohn/Trellis) framework** — by having AI implement real specs end-to-end while you study idiomatic, production-shaped code.

Pick a business spec, hand it to an AI (GitHub Copilot or any model you like), and watch it build a complete service on Trellis. Then **read it, run it, and review it** against a checklist. You walk away understanding how a real Trellis service is shaped — Clean Architecture layers, Railway-Oriented error handling, value objects, state machines, versioned APIs, and EF Core conventions — without staring at a blank page.

> 🧪 **It doubles as a benchmark.** Because every lab is scored against the same rubric, running it across different AI models also measures how consistently Trellis steers them. That's a handy side effect — see [Also a benchmark](#also-a-benchmark-measuring-ai-consistency) — but if you're here to **learn Trellis, jump straight to [Quick Start](#quick-start).**

---

## What is Trellis?

[Trellis](https://github.com/xavierjohn/Trellis) is a .NET framework for building enterprise services with strong domain modeling and explicit error handling. Instead of throwing exceptions for expected failures, you compose `Result<T>` and `Maybe<T>` pipelines (Railway-Oriented Programming). Instead of passing raw `Guid` / `string` / `int` around, you model domain concepts as value objects (`RequiredGuid<T>`, `RequiredString<T>`, `RequiredEnum<T>`, …). On top of that it ships DDD building blocks — aggregates, entities, specifications, state machines — plus first-class ASP.NET Core, EF Core, and Mediator integration. **These labs teach those building blocks by example.**

<p align="center">
  <img src="docs/images/architecture-overview.png" alt="Clean Architecture — API, Anti-Corruption Layer, Application, Domain" width="700"/>
</p>

## What you'll learn

Completing a lab shows you, in working code, how Trellis shapes:

- **Clean Architecture** — `API → Anti-Corruption Layer → Application → Domain`, with dependencies pointing inward.
- **Railway-Oriented Programming** — `Result<T>` / `Maybe<T>` chains (`Bind` / `Map` / `Ensure`) with no try/catch on the happy path.
- **Rich domain modeling** — value objects, `RequiredEnum<T>` smart enums, aggregates, entities, and specifications.
- **State machines** — `LazyStateMachine` driving an order lifecycle with guarded transitions and stock side effects.
- **Versioned HTTP APIs** — namespace-based API versioning, RFC 9457 ProblemDetails, ETags / `If-Match`.
- **EF Core, the Trellis way** — conventions, interceptors, and Unit-of-Work commits (handlers never call `SaveChanges`).
- **Authorization & testing** — actor-based authorization and the `Trellis.Testing` assertion helpers.

<p align="center">
  <img src="docs/images/order-lifecycle.png" alt="Order State Machine — Draft through Delivered with Cancel transitions" width="600"/>
</p>

## Prerequisites

- .NET 10 SDK
- VS Code or Visual Studio
- GitHub Copilot (Copilot Chat in VS Code) — or another AI model you want to drive the build
- The Trellis ASP template: `dotnet new install Trellis.AspTemplate`
- Docker Desktop *(optional — for the Aspire Dashboard)*
- The Trellis Microservices template *(optional — for future multi-service labs)*: `dotnet new install Trellis.Microservices.Templates`

## Quick Start

```bash
# 1. Clone this repo
git clone https://github.com/xavierjohn/Trellis-training.git

# 2. Install the Trellis template
dotnet new install Trellis.AspTemplate

# 3. Open the operator guide for the lab you want to learn:
#    - HTTP CRUD + state machine:  docs/training-lab.md          (Order Management — start here)
#    - Background worker:          docs/training-lab-worker.md    (Subscription Reminder)

# 4. Follow Steps 1-8 in that guide. The implementation itself (Step 4) happens
#    by pasting the lab spec + checklist into GitHub Copilot — the AI writes the
#    code; you read, run, and review it against the checklist.
```

New here? **Start with the Order Management lab ([`docs/training-lab.md`](docs/training-lab.md))** — it's the canonical, fully-documented walkthrough. Prefer to just *read* finished code first? Jump to [Study the reference implementation](#study-the-reference-implementation).

## How a lab works

Every lab follows the same 8-step procedure. The Order Management guide ([`docs/training-lab.md`](docs/training-lab.md)) is the canonical reference; per-lab guides add or override steps where the system shape requires it.

<p align="center">
  <img src="docs/images/step-flow.png" alt="8 steps — Create Project, Aspire Dashboard, Scaffold, AI Implements, Smoke Test, Review, Feedback, Add Feature" width="700"/>
</p>

| Step | What happens | Time |
|------|-------------|------|
| **1** | Create project directory | 1 min |
| **2** | Start Aspire Dashboard for observability | 2 min |
| **3** | Scaffold with `dotnet new trellis-asp` (or `dotnet new trellis-microservices` for multi-service labs) | 2 min |
| **4** | Paste lab spec + checklist into Copilot — AI implements everything | 10-30 min |
| **5** | Manual smoke test (`.http` file for HTTP labs; `/health` polling for worker labs) | 5 min |
| **6** | Review generated code | 5 min |
| **7** | AI generates `TRELLIS_FEEDBACK.md` | 2 min |
| **8** | AI adds an incremental feature — **OM lab only** (Order Returns). The worker and URL-shortener labs are single-shot (Steps 1–7). | 10-15 min |

**Total: ~45 minutes per run** for the OM lab; the single-shot worker and URL-shortener labs run ~30. Each operator guide names the lab-specific Step 4 attachments and Step 5 smoke verification.

## Lab catalog

Each lab targets a different **system shape**, so you learn how Trellis handles a different kind of service. Start with Order Management, then branch out.

| Lab | What you'll learn (system shape) | Spec | Operator guide |
|---|---|---|---|
| **Order Management** | CRUD + state machine + versioned API + EF Core | [`specs/order-management.md`](specs/order-management.md) | [`docs/training-lab.md`](docs/training-lab.md) |
| **Subscription Reminder Worker** | `BackgroundService` + scheduled work + non-HTTP pipeline + cross-pipeline actor composition | [`specs/subscription-reminder-worker.md`](specs/subscription-reminder-worker.md) | [`docs/training-lab-worker.md`](docs/training-lab-worker.md) |
| **URL Shortener** | Unversioned HTTP + write-then-redirect + `Idempotency-Key` + ETag + anonymous redirect alongside permission-gated CRUD | [`specs/url-shortener.md`](specs/url-shortener.md) | [`docs/training-lab-url-shortener.md`](docs/training-lab-url-shortener.md) |

> Checklists live alongside the specs: the OM checklist is embedded in its operator guide; the worker and URL-shortener labs use the [`specs/coverage-checklist-*.md`](specs/) files.

## Study the reference implementation

Want to read idiomatic Trellis code without running anything? Two complete copies of the Order Management lab are checked in:

- **[`before/OrderManagement/`](before/OrderManagement/)** — the template scaffold you start from (the sample `WeatherForecast` service).
- **[`after/OrderManagement/`](after/OrderManagement/)** — a complete, passing reference implementation. Start in `Domain/src/` (value objects, aggregates, the order state machine) and follow the layers outward through `Application/src/`, `Acl/src/`, and `Api/src/`.

<p align="center">
  <img src="docs/images/before-after.png" alt="Before and After — from template scaffold to a full Trellis service" width="700"/>
</p>

Trellis leans on Railway-Oriented Programming throughout: every handler threads a `Result<T>` so failures short-circuit without exceptions, and the commit is a framework pipeline stage rather than a `SaveChanges` call in the handler.

<p align="center">
  <img src="docs/images/rop-pipeline.png" alt="Railway-Oriented Programming — Result chains flowing through a handler" width="600"/>
</p>

## Observability

Every lab includes Aspire Dashboard integration for real-time traces, metrics, and structured logs.

<p align="center">
  <img src="docs/images/aspire-dashboard.png" alt="Aspire Dashboard — distributed traces showing service calls" width="700"/>
</p>

The HTTP labs serve interactive API docs via Scalar:

<p align="center">
  <img src="docs/images/scalar-api-docs.png" alt="Scalar API documentation — interactive OpenAPI explorer" width="700"/>
</p>

## Also a benchmark: measuring AI consistency

The training lab has a useful side effect. Because every lab is scored against the same per-lab checklist, running a spec across **different AI models** measures whether Trellis constrains them into the *same* architecture, patterns, and error handling.

You're **not** measuring whether AI can write code — you're measuring whether **Trellis is a tight enough framework** that independent runs converge. Where runs diverge, Trellis needs a tighter building block; where divergence is consistent across labs, the framework has a structural gap. Keeping the corpus heterogeneous (HTTP CRUD, worker, redirect host, …) keeps that signal from being biased toward whatever lab was written first.

### Scoring

The OM lab uses a 57-criteria rubric across five levels:

| Level | What it measures | OM rows |
|-------|-----------------|---------|
| **L1: Structural** | Are the right types, patterns, and building blocks present? | 18 |
| **L2: Behavioral** | Does the business logic work correctly? | 13 |
| **L3: Architecture** | Is the API, DI, and infrastructure correct? | 13 |
| **L4: Tests** | Are domain, integration, and auth tests comprehensive? | 9 |
| **L5: Feedback** | Did the AI produce useful framework feedback? | 4 |

**Passing score (OM lab): 52+/57.** Per-lab thresholds live in each operator guide.

<p align="center">
  <img src="docs/images/evaluation-radar.png" alt="Evaluation radar — L1 Structural, L2 Behavioral, L3 Architecture, L4 Tests, L5 Feedback" width="500"/>
</p>

### Order Management baselines — Trellis `3.0.0-alpha.360`

| AI Model | Score | Verdict |
|----------|-------|---------|
| Claude Opus 4.8 | 56/57 (98%) | **PASS** |
| Claude Sonnet 4.6 | 56/57 (98%) | **PASS** |
| Claude Opus 4.7 (1M ctx) | 55/57 (96%) | **PASS** |
| GPT-5.5 | 54/57 (95%) | **PASS** |
| Claude Haiku 4.5 | 43/57 (75%) | **FAIL** |

The reference run checked into [`after/OrderManagement/`](after/OrderManagement/) is the Opus 4.7 1M result. Full per-model scorecards (with criterion-by-criterion findings and the rubric's change history) live in **[results/evaluation-results.md](results/evaluation-results.md)**; recurring mistake patterns are tracked in **[results/ai-mistakes-log.md](results/ai-mistakes-log.md)**.

## Repository Structure

```
Trellis-training/
├── README.md                                            # This file
├── docs/
│   ├── training-lab.md                                  # OM lab — operator guide + rubric
│   ├── training-lab-worker.md                           # Subscription-reminder worker — operator guide
│   ├── training-lab-url-shortener.md                    # URL shortener — operator guide
│   └── images/                                          # Visual assets
├── specs/                                               # Lab specs (paste into Copilot)
│   ├── order-management.md
│   ├── subscription-reminder-worker.md
│   ├── coverage-checklist-subscription-reminder.md
│   ├── url-shortener.md
│   └── coverage-checklist-url-shortener.md
├── results/
│   ├── evaluation-results.md                            # Historical scorecards (OM lab)
│   └── ai-mistakes-log.md                               # Common mistake patterns
├── before/
│   └── OrderManagement/                                 # Template scaffold (what you start with)
└── after/
    └── OrderManagement/                                 # Reference implementation (what AI builds)
```

## Related repositories

- [`xavierjohn/Trellis`](https://github.com/xavierjohn/Trellis) — the framework you're learning: `Result<T>`, `Maybe<T>`, value objects, DDD primitives, ASP.NET / EF Core / Mediator integration.
- [`xavierjohn/Trellis.AspTemplate`](https://github.com/xavierjohn/Trellis.AspTemplate) — `dotnet new trellis-asp` single-service Clean Architecture template used by the OM, worker, and URL-shortener labs.
- [`xavierjohn/Trellis.Microservices`](https://github.com/xavierjohn/Trellis.Microservices) — microservice trust-boundary packages: YARP gateway + consumer-side actor provider.
- [`xavierjohn/Trellis.Microservices.Template`](https://github.com/xavierjohn/Trellis.Microservices.Template) — `dotnet new trellis-microservices` multi-service Project Tracker template. A future multi-service lab will benchmark AI consistency across the gateway + downstream services topology.
- [`xavierjohn/Trellis.ServiceLevelIndicators`](https://github.com/xavierjohn/Trellis.ServiceLevelIndicators) — latency SLI metrics library. The OM and URL-shortener labs already emit `Trellis.SLI`-shaped metrics via the framework's middleware.

## License

[MIT](LICENSE)

---

<p align="center">
  <b>Built with <a href="https://github.com/xavierjohn/Trellis">Trellis</a></b> — the framework for building enterprise .NET services with AI.
</p>
