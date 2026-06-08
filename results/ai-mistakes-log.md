# AI Mistakes & Self-Corrections Log

Tracks mistakes AI models make during implementation — both self-corrected and human-fixed — to identify **generalizable** instruction improvements.

**Goal:** Find patterns that indicate missing guidance in copilot instructions, without curve-fitting to the Order Management spec.

**Curve-fitting guard:** After completing this evaluation round, new specs (different domain, different complexity) will be used to validate any instruction changes.

---

## How to Use This Document

After each AI run, review the git diff and agent conversation to log:

1. **Self-corrected mistakes** — AI wrote something wrong, then fixed it in a subsequent edit
2. **Human-fixed mistakes** — User had to intervene or the mistake remained in final output
3. **Persistent mistakes** — Multiple models make the same mistake independently

For each entry, assess whether an instruction improvement would be **generalizable** (helps across all specs) or **spec-specific** (only helps with this particular exercise).

---

## Mistake Categories

| Category | Description | Example |
|----------|-------------|---------|
| **API Surface** | Wrong type name, namespace, or method signature | `AggregateRoot<>` instead of `Aggregate<>` |
| **Pattern** | Right concept, wrong implementation pattern | Hand-coded state machine instead of Stateless |
| **Convention** | Ignored or misapplied a convention from instructions | `HasConversion()` instead of `ApplyTrellisConventions` |
| **Architecture** | Wrong layer, wrong dependency direction | Repository interface in Domain instead of Application |
| **Test** | Missing tests, wrong test pattern, deleted template tests | Deleted template tests without replacement |
| **Build System** | Modified build files that shouldn't be touched | Changed Directory.Build.props |
| **Omission** | Forgot a required artifact entirely | No TRELLIS_FEEDBACK.md |
| **Primitive Obsession** | Used raw types instead of value objects | `string email` instead of `EmailAddress` |
| **Reinvention** | Created custom type that already exists in framework | Custom `EmailAddress` when `Trellis.Primitives.EmailAddress` exists |

---

## Logged Mistakes

### Template — fill in after each run:

```
#### [Mistake Title]
- **Category:** [from table above]
- **Models affected:** [which models made this mistake]
- **Self-corrected?** Yes / No / Partial
- **What happened:** [brief description]
- **Root cause:** [why the AI made this mistake]
- **Generalizable?** Yes / No
- **Potential instruction fix:** [what could be added/changed — or "none, spec-specific"]
```

---

### Reinvention: Custom EmailAddress / PhoneNumber

- **Category:** Reinvention
- **Models affected:** [To be filled — observed across multiple models per user report]
- **Self-corrected?** No
- **What happened:** AI created custom `EmailAddress` and `PhoneNumber` value objects instead of using the built-in ones from `Trellis.Primitives`.
- **Root cause:** The copilot instructions didn't explicitly list which value objects are already provided by Trellis.
- **Generalizable?** Yes — any spec with email/phone fields would hit this.
- **Potential instruction fix:** ✅ **Already fixed** — Added principle #4: "Use built-in `Trellis.Primitives` before creating custom value objects" with explicit type list.

---

### Opus 4.6: No ParallelAsync for parallel product fetching

- **Category:** Pattern
- **Models affected:** Opus 4.6, Sonnet 4, Gemini 2.5 Pro Run 1, GPT-5.2 Codex Max
- **Self-corrected?** No
- **What happened:** CreateDraftOrderHandler loads customer then fetches products sequentially in a loop instead of using `Result.ParallelAsync()` to fetch them concurrently.
- **Root cause:** `ParallelAsync` is not prominently documented. Models default to sequential async patterns.
- **Generalizable?** Yes — any handler with multiple independent async loads would benefit.
- **Potential instruction fix:** Add `ParallelAsync` to the "key Result extensions" section with a usage example.

### Opus 4.6: No IEntityTypeConfiguration classes

- **Category:** Convention
- **Models affected:** Opus 4.6, GPT-5.2 Codex Max
- **Self-corrected?** No
- **What happened:** All EF Core entity configuration is inline in `DbContext.OnModelCreating()` instead of separate `IEntityTypeConfiguration<T>` classes per entity.
- **Root cause:** Instructions don't explicitly require per-entity configuration classes; template uses inline config.
- **Generalizable?** Yes — any spec with multiple entities benefits from separated configuration.
- **Potential instruction fix:** Add note that entity configuration should use `IEntityTypeConfiguration<T>` per entity.

### Opus 4.6: Blocking .GetAwaiter().GetResult() in SubmitOrderHandler

- **Category:** Pattern
- **Models affected:** Opus 4.6
- **Self-corrected?** No
- **What happened:** `SubmitOrderHandler` uses `.GetAwaiter().GetResult()` to call async `productRepository.GetByIdAsync()` inside a sync `Func<ProductId, int, Result<Unit>>` delegate passed to `Order.Submit()`.
- **Root cause:** Domain's `Submit()` takes a sync `Func<>` delegate, but the repository is async. The handler pragmatically bridges the gap with blocking call.
- **Generalizable?** Partially — the domain Submit signature design itself forces this. Consider async delegate or accept blocking.
- **Potential instruction fix:** None — this is a domain design constraint. Future specs could show async delegate pattern as alternative.

---

### Sonnet 4.6 Run 2: No Bind/BindAsync — Imperative error handling throughout

- **Category:** Pattern
- **Models affected:** Sonnet 4.6 Run 2
- **Self-corrected?** No
- **What happened:** All handlers use imperative `if (!result.TryGetValue(out var value)) { _ = result.TryGetError(out var error); return error; }` patterns instead of ROP chains with `Bind`/`BindAsync`/`Map`/`Tap`. The `Bind` API from `Trellis.Core` (which provides `Result<T>` and the pipeline operators — formerly distributed as `Trellis.Results`) is not used anywhere in custom code (only in template's WeatherForecastController).
- **Root cause:** Per the model's TRELLIS_FEEDBACK.md (FP-1), the TRLS004 analyzer does not recognize `!TryGetValue(out var v)` as an `IsFailure` guard, forcing the verbose `TryGetError` workaround. The model likely attempted Bind first, hit the analyzer friction, and fell back to the imperative pattern across all handlers.
- **Generalizable?** Yes — any handler composing multiple Result operations benefits from Bind/BindAsync chains. The analyzer gap is a real framework issue.
- **Potential instruction fix:** Two options: (1) Add explicit Bind/BindAsync usage examples in the copilot instructions handler section, showing the preferred ROP chain pattern. (2) Fix TRLS004 analyzer to recognize `!TryGetValue` as an `IsFailure` guard, reducing the friction that pushes models to imperative fallback.

### Sonnet 4.6 Run 2: int StockQuantity — Primitive obsession for stock

- **Category:** Primitive Obsession
- **Models affected:** Sonnet 4.6 Run 2
- **Self-corrected?** No
- **What happened:** `Product.StockQuantity` is raw `int` and `AddStock(int quantity)` takes a raw int parameter. Other stock methods (`ReserveStock`, `ReleaseStock`) correctly use the typed `Quantity` value object, creating an inconsistency.
- **Root cause:** The spec doesn't explicitly require a `StockQuantity` VO. The model created `Quantity` for line items but didn't create a separate `StockQuantity` for product inventory.
- **Generalizable?** Partially — "no raw types in domain" is a general DDD principle, but which exact types need wrapping depends on the domain.
- **Potential instruction fix:** Add guidance: "Every numeric or string property on an Aggregate or Entity should be a typed value object. If the same concept appears in two contexts (e.g., line item quantity vs stock quantity), consider whether they need separate types."

---

## Cross-Model Patterns

*After multiple runs, summarize recurring patterns here.*

| Pattern | Frequency | Models | Generalizable Fix | Status |
|---------|-----------|--------|-------------------|--------|
| Custom EmailAddress/PhoneNumber | Multiple | Sonnet 4, Gemini (both), GPT-5.2* | List built-in primitives in instructions | ✅ Fixed |
| No Stateless state machine | 4/6 models | Sonnet 4, Gemini (both), GPT-5.2 | ? | Needs analysis |
| No Specification\<Order\> | 3/6 models | GPT-5.2, Sonnet 4 | ? | Needs analysis |
| Deleted template tests | 2/6 models | GPT-5.2, Gemini 2.5 Pro Run 2 | ? | Needs analysis |
| No TRELLIS_FEEDBACK.md | 2/6 models | Sonnet 4, GPT-5.2 | ? | Needs analysis |
| No ParallelAsync | 6/6 models | All tested (including Opus 4.6, Sonnet 4.6 R2) | Add ParallelAsync to key extensions with example | ✅ Fixed |
| No IEntityTypeConfiguration | 2/6 models | Opus 4.6, GPT-5.2 | Add explicit convention to instructions | ✅ Fixed |
| Inline DbContext config | 2/6 models | Opus 4.6, GPT-5.2 | Template uses inline — consider changing template | ✅ Fixed (instructions) |
| No Bind/BindAsync usage | 1/6 models | Sonnet 4.6 Run 2 | Add Bind examples + fix TRLS004 analyzer | ✅ Fixed (instructions) |
| Primitive obsession (StockQuantity) | 1/6 models | Sonnet 4.6 Run 2 | Add VO-per-property guidance | ✅ Fixed |

\* GPT-5.2 used built-in Primitives correctly (instructions already fixed before its run)

---

## Instruction Changes Made

Track changes to copilot instructions that resulted from this analysis.

| Date | Change | Triggered By | Generalizable? |
|------|--------|--------------|----------------|
| 2026-03-03 | Added principle #4: list built-in Trellis.Primitives types | Multiple models creating custom EmailAddress/PhoneNumber | Yes |
| 2026-03-03 | Strengthened principle #3: VO-per-property on aggregates/entities | Sonnet 4.6 R2 `int StockQuantity` primitive obsession | Yes |
| 2026-03-03 | Added Handler ROP Pattern section: Bind/BindAsync chains vs imperative unwrapping | Sonnet 4.6 R2 imperative TryGetValue/TryGetError pattern | Yes |
| 2026-03-03 | Added Parallel Async section: ParallelAsync + WhenAllAsync with code example | 0/6 models used ParallelAsync for concurrent fetches | Yes |
| 2026-03-03 | Added IEntityTypeConfiguration convention: per-entity config files + ApplyConfigurationsFromAssembly | 4/6 models used inline OnModelCreating config | Yes |
| 2026-03-03 | Added migration instruction: `dotnet ef migrations add InitialCreate` | 0/6 models created a migration | Yes |

