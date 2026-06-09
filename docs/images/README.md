# Image Assets — Placeholders for Open Art

Each image below needs to be created and placed in this folder. The README.md references them by filename.

## Required Images

### 1. `hero-banner.png` (800×300)
**Used in:** README.md hero section
**Concept:** Wide banner showing the intersection of AI and enterprise software development. Think: a neural network or circuit board pattern morphing into clean architectural blueprints. Color palette: deep navy, electric blue, white accents.
**Text overlay:** "Trellis Training Lab" + "AI-Powered Enterprise Service Development"

### 2. `architecture-overview.png` (landscape, displayed at width 700)
**Used in:** README.md header (above the lab catalog)
**Concept:** The 4-layer clean architecture diagram, stacked outer → inner to match the
actual `*.csproj` dependency graph in `after/OrderManagement`:
- **API** (top, blue) — Controllers, DTOs, Middleware
- **Anti-Corruption Layer** (orange) — EF Core (DbContext), Repositories, Configurations
- **Application** (green) — Commands, Queries, Handlers, Authorization, Repository Interfaces
- **Domain** (purple, bottom) — Aggregates, Entities, Value Objects, Events, Specifications

Arrows point in the direction of dependency (outer layers depend inward): `API → ACL → Application → Domain`,
plus an `API → Application` skip arrow. The Anti-Corruption Layer is an **outer** layer — it implements
the repository interfaces declared in Application (Dependency Inversion), so it depends on Application
rather than sitting between Application and Domain.

Regenerate with [`gen_architecture.py`](gen_architecture.py) (`python docs/images/gen_architecture.py`);
source of truth is the `ProjectReference` graph under `after/OrderManagement`.

### 3. `order-lifecycle.png` (landscape, displayed at width 600)
**Used in:** README.md header (above "What you're measuring")
**Concept:** State machine diagram for the Order lifecycle (mirrors spec section 4 and
`after/OrderManagement/Domain/src/Aggregates/Order.cs`):
```
Draft → Submitted → Approved → Shipped → Delivered
  ↓         ↓           ↓
Cancel   Cancel      Cancel
```
Each state as a rounded rectangle with color coding (Draft=gray, Submitted=blue, Approved=green, Shipped=orange, Delivered=purple, Cancelled=red). Transitions as labeled arrows. Show **"Reserve Stock" on the Submit arrow** and **"Release Stock" only on the Submitted→Cancelled and Approved→Cancelled arrows** — cancelling a Draft reserves nothing, so it releases nothing (spec 4: *"If order was Submitted or Approved, release reserved stock"*).

Regenerate with [`gen_order_lifecycle.py`](gen_order_lifecycle.py) (`python docs/images/gen_order_lifecycle.py`).

### 4. `before-after.png` (700×400)
**Used in:** README.md "Before & After" section
**Concept:** Split-screen comparison:
- **Left side** labeled "Before (Template)" — shows a simple project tree with sample WeatherForecast code, faded/gray tone
- **Right side** labeled "After (AI-Generated)" — shows the full Order Management project tree with real domain types, vibrant/colorful tone
- Visual arrow or transform icon between them
- Emphasize the transformation from boilerplate to real enterprise code

### 5. `step-flow.png` (landscape, displayed at width 700)
**Used in:** README.md "How a lab works" section
**Concept:** Horizontal flow diagram showing the 8 lab steps (wording follows the README
8-step table):
```
① Create Project → ② Aspire Dashboard → ③ Scaffold → ④ AI Implements → ⑤ Smoke Test → ⑥ Review → ⑦ Feedback → ⑧ Add Feature
```
Each step as a numbered circle inside a card, connected by arrows. Steps 4 and 8 are highlighted green (the AI-driven steps). Step 8 is intentionally lab-agnostic ("Add Feature") because the feature differs per lab (OM: Order Returns, worker: SLA policy override, URL shortener: bulk-import endpoint). Clean, minimal design.

Regenerate with [`gen_step_flow.py`](gen_step_flow.py) (`python docs/images/gen_step_flow.py`).

### 6. `evaluation-radar.png` (landscape, displayed at width 500)
**Used in:** README.md "Evaluation" section
**Concept:** Radar/spider chart with 5 axes, each normalized so the outer ring is full marks:
- L1: Structural (18 pts)
- L2: Behavioral (13 pts)
- L3: Architecture (13 pts)
- L4: Tests (9 pts)
- L5: Feedback (4 pts)

Overlay one polygon per model in the current alpha.360 cohort — Opus 4.8, Sonnet 4.6, Opus 4.7 1M, GPT-5.5, and Haiku 4.5 — with a legend showing each model's total score. The four PASS models cluster near the outer ring; Haiku 4.5 (the only FAIL) visibly pulls in on L3 and L5.

Regenerate with [`gen_radar.py`](gen_radar.py) (`python docs/images/gen_radar.py`); source data is the "Current cohort" table in [`results/evaluation-results.md`](../../results/evaluation-results.md).

### 7. `aspire-dashboard.png` (700×400)
**Used in:** README.md "Observability" section
**Concept:** Screenshot or stylized mockup of the .NET Aspire Dashboard showing:
- Distributed traces for an Order Management API call chain
- Spans showing: Controller → Mediator → Handler → Repository → EF Core
- Timing information visible
- The Aspire Dashboard dark theme

**Note:** This is best as an actual screenshot. Run the service, execute a few API calls, then screenshot the traces view.

### 8. `scalar-api-docs.png` (700×400)
**Used in:** README.md "Observability" section
**Concept:** Screenshot or stylized mockup of Scalar API documentation showing:
- The Order Management API endpoints listed
- One endpoint expanded (e.g., POST /api/Orders)
- Request/response schema visible
- The Scalar dark theme with clean typography

**Note:** Best as an actual screenshot from `https://localhost:7011/scalar/2026-11-12` after running the service.

### 9. `rop-pipeline.png` (600×250)
**Used in:** README.md bottom section
**Concept:** Railway-oriented programming visualization. Show two parallel tracks (Success/Failure) with operations chained:
```
GetOrder → Submit → SaveAsync → MapToDto
   ↓          ↓         ↓          ↓
 NotFound → Invalid  → DbError → [propagates]
```
Success track on top (green/blue), failure track on bottom (red). Each operation is a box that can route to either track. Show how errors propagate without try/catch.

---

## Style Guidelines

- **Color palette (from Trellis brand):** Navy (#1a2744), Dark Green (#1b5e3a), Teal (#4db8a4), Light Green (#8cc63f), White (#ffffff)
- **Style:** Clean, modern, slightly technical — think developer documentation meets infographic. Use the trellis lattice + vine motif where appropriate.
- **Fonts:** Sans-serif (condensed for headings, matching the "THE TRELLIS" logo style), high contrast for readability at small sizes
- **Format:** PNG with white or transparent backgrounds (works on both light/dark GitHub themes)
- **Resolution:** 2x for retina displays (actual dimensions in the table above are display size)
