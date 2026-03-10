# Image Assets — Placeholders for Open Art

Each image below needs to be created and placed in this folder. The README.md references them by filename.

## Required Images

### 1. `hero-banner.png` (800×300)
**Used in:** README.md hero section
**Concept:** Wide banner showing the intersection of AI and enterprise software development. Think: a neural network or circuit board pattern morphing into clean architectural blueprints. Color palette: deep navy, electric blue, white accents.
**Text overlay:** "Trellis Training Lab" + "AI-Powered Enterprise Service Development"

### 2. `architecture-overview.png` (700×400)
**Used in:** README.md "What Gets Built" section
**Concept:** The 4-layer clean architecture diagram. Four horizontal layers stacked:
- **API** (top, blue) — Controllers, DTOs, Middleware
- **Application** (green) — Commands, Queries, Handlers, Authorization
- **Anti-Corruption Layer** (orange) — EF Core, Repositories, Configurations
- **Domain** (purple, bottom) — Aggregates, Entities, Value Objects, Events, Specifications

Arrows showing dependency direction (outer layers depend inward). Each layer shows key types it contains.

### 3. `order-lifecycle.png` (600×350)
**Used in:** README.md "What Gets Built" section
**Concept:** State machine diagram for Order lifecycle:
```
Draft → Submitted → Approved → Shipped → Delivered
  ↓         ↓           ↓
Cancel   Cancel      Cancel
```
Each state as a rounded rectangle with color coding (Draft=gray, Submitted=blue, Approved=green, Shipped=orange, Delivered=purple, Cancelled=red). Transitions as labeled arrows. Show "Reserve Stock" on Submit arrow, "Release Stock" on Cancel arrows.

### 4. `before-after.png` (700×400)
**Used in:** README.md "Before & After" section
**Concept:** Split-screen comparison:
- **Left side** labeled "Before (Template)" — shows a simple project tree with sample WeatherForecast code, faded/gray tone
- **Right side** labeled "After (AI-Generated)" — shows the full Order Management project tree with real domain types, vibrant/colorful tone
- Visual arrow or transform icon between them
- Emphasize the transformation from boilerplate to real enterprise code

### 5. `step-flow.png` (700×200)
**Used in:** README.md "How It Works" section
**Concept:** Horizontal flow diagram showing 8 steps:
```
① Scaffold → ② Dashboard → ③ Template → ④ AI Implements → ⑤ Smoke Test → ⑥ Review → ⑦ Feedback → ⑧ Returns Feature
```
Each step as a numbered circle or card connected by arrows. Steps 4 and 8 highlighted (these are the AI-driven steps). Clean, minimal design.

### 6. `evaluation-radar.png` (500×500)
**Used in:** README.md "Evaluation" section
**Concept:** Radar/spider chart with 5 axes:
- L1: Structural (18 pts)
- L2: Behavioral (13 pts)
- L3: Architecture (13 pts)
- L4: Tests (9 pts)
- L5: Feedback (4 pts)

Show 2-3 overlaid polygons representing different AI models (e.g., Opus 4.6 filling most of the chart, Sonnet 4 filling much less). Include a legend. Colors: Opus=blue fill, Sonnet=gray fill.

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

- **Color palette:** Navy (#1a1a2e), Electric Blue (#0099ff), White (#ffffff), with accents in Green (#00cc66), Orange (#ff9933), Purple (#9966ff), Red (#ff4444)
- **Style:** Clean, modern, slightly technical — think developer documentation meets infographic
- **Fonts:** Sans-serif, high contrast for readability at small sizes
- **Format:** PNG with transparent or dark backgrounds (works on both light/dark GitHub themes)
- **Resolution:** 2x for retina displays (actual dimensions in the table above are display size)
