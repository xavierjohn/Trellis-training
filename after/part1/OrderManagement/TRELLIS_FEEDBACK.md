# Trellis Framework Feedback

Observations while implementing a complete Order Management service with Trellis on .NET 10.

## What Worked Well

### Railway-Oriented Programming Integration
- `.TapAsync()` / `.Map()` / `.BindAsync()` chaining is natural and eliminates defensive null/error checks throughout handler code.
- `Result.Combine(r1, r2, r3)` → flat tuple `(T1, T2, T3)` is ergonomic once you know the flat-tuple behavior.
- `Result.ParallelAsync().WhenAllAsync()` made concurrent repository lookups easy.

### Analyzers (Trellis.Analyzers)
- **TRLS001** and **TRLS003** catch real bugs (unhandled results, unsafe `.Value` access) at compile time. They are strict but valuable.
- The analyzers fire in test projects too, which forces honest test code rather than "test shortcuts" that bypass error handling.

### EF Core Generator
- `ApplyTrellisConventions()` in `ConfigureConventions` and `ApplyConfigurationsFromAssembly` eliminated repetitive column mapping.
- `HasTrellisIndex(o => new { o.Status, o.SubmittedAt })` correctly resolved the backing field `_submittedAt` for `Maybe<DateTime>` properties.
- `MaybeQueryInterceptor` enabled `Maybe<T>` properties in LINQ `Where` clauses that translate to SQL.

### Resource Authorization
- `IAuthorizeResource<T>` + `ResourceLoaderById<TCommand, TResource, TId>` is clean. Separating permission checks from resource-specific checks via `IAuthorize` + `IAuthorizeResource<T>` is elegant.
- `AddResourceAuthorization(commandAssembly, resourceLoaderAssembly)` in ACL DI wires up everything.

### Value Objects
- `RequiredGuid<T>`, `RequiredString<T>`, `RequiredEnum<T>` cover most domain value object needs with minimal boilerplate.
- `[StringLength(n)]` on `RequiredString<T>` subclasses uses Trellis's own attribute — avoids needing `System.ComponentModel.DataAnnotations`.

### LazyStateMachine
- `LazyStateMachine<TState>` backed by `Stateless` makes state machine definition declarative.
- `.FireResult()` returns `Result<TState>` so transitions compose with the ROP pipeline.

---

## Pain Points / Suggestions

### 1. `partial` Requirement Is Implicit
**Issue:** Classes with `partial Maybe<T>` properties must be declared `public partial class`, but there is no compile-time error explaining why. The generated code fails to compile with a confusing "partial member not in a partial type" error.

**Suggestion:** The Trellis.EntityFrameworkCore.Generator analyzer should emit a diagnostic (e.g., TRLS-ECXX) saying "Type 'Foo' must be declared `partial` because it contains partial Maybe<T> properties."

### 2. No `OrderStatus.Create(string)` — Inconsistent API
**Issue:** Some `RequiredEnum<T>` uses have a `.Create(string)` factory and some don't. In version 3.0.0-alpha.124, `OrderStatus.Create(state)` does not exist. The alternative `TryFromName(state)` works but is less discoverable.

**Suggestion:** Document whether `Create(string)` is always available on `RequiredEnum<T>`, or add it consistently.

### 3. `Unit.Value` Is Unavailable in Test Projects
**Issue:** `Unit.Value` compiles in the main Application project but not in the test project, with error `CS0117: 'Unit' does not contain a definition for 'Value'`. The ambiguity arises because the test project pulls in both `Trellis.Primitives.Unit` and possibly another `Unit` via transitive dependencies.

**Workaround:** Use `Result.Success()` (parameterless) instead of `Result.Success(Unit.Value)`.

**Suggestion:** Prefer `Result.Success()` everywhere and deprecate `Unit.Value` patterns. The reference docs do call out this preference but `Unit.Value` still appears in example code.

### 4. `Maybe<T>.From(value)` Is on a Static Helper Class, Not the Generic Struct
**Issue:** `Maybe<Customer>.From(customer)` does not compile. The correct API is `Maybe.From(customer)` (non-generic static helper). This is surprising because `.None` IS on the generic struct as `Maybe<Customer>.None`.

**Suggestion:** Either add `static Maybe<T> From(T value)` on the `Maybe<T>` struct itself, or document the asymmetry prominently.

### 5. TRLS003 Fires Even After `Should().BeSuccess()` Guard in Tests
**Issue:** The analyzer does not recognize `result.Should().BeSuccess()` as a guard before `result.Value`. Calls like:
```csharp
result.Should().BeSuccess();
var value = result.Value; // TRLS003 still fires
```
require replacing with `result.TryGetValue(out var value)` which is less readable in test assertion code.

**Suggestion:** Consider suppressing TRLS003 in test projects, or recognize FluentAssertions `Should().BeSuccess()` as a guard clause. Alternatively, provide a `result.GetValueOrThrow()` method explicitly designed for tests that satisfies the analyzer.

### 6. `Result.Combine` Flat Tuple Behavior Is Underdocumented
**Issue:** `r1.Combine(r2).Combine(r3)` produces `Result<(T1, T2, T3)>` (flat), not `Result<((T1, T2), T3)>` (nested). This is the right behavior but it's easy to write deconstruct as `var ((a, b), c) = values` when it should be `var (a, b, c) = values`.

**Suggestion:** Add a note in the documentation and/or an analyzer hint when deconstruction depth doesn't match.

### 7. `IResult` Return Type in `IAuthorize` Methods
**Issue:** `IAuthorize.Authorize()` returns `IResult`, but `Error.Forbidden(...)` returns `ForbiddenError` (not `IResult`). You must explicitly wrap: `return Result.Failure(Error.Forbidden(...))`. This creates a cognitive mismatch — the method signature implies you return an error, but you actually return a `Result`.

**Suggestion:** Either change the return type to `Error?` (return null for success, an error for failure), or add an explicit `Result.Forbid(...)` / `Result.Unauthorized(...)` convenience factory.

### 8. No `AddStock` on Draft Order Creation
**Issue:** When creating a draft order via `CreateDraftOrderCommand`, products must already be in the repository with stock. The handler doesn't check stock at draft-creation time (only at submit time). This is correct domain behavior, but the test setup needs to call `AddStock` before adding the product to the repository, with no compile-time reminder to do so.

This is more of a documentation/modeling note than a framework issue.

---

## Trellis Version Used
- Package version: `3.0.0-alpha.124` (from `Directory.Packages.props`)
- .NET version: `net10.0`
