# TRELLIS_FEEDBACK.md — Order Management benchmark run

This document captures friction and bugs hit while AI-generating the Order Management sample (`after/OrderManagement/`) against `Trellis` 3.0.0-alpha.360 with Trellis.AspTemplate 1.0.19-alpha (Claude Opus 4.7 1M context, 2026-06-08).

The lab spec is `specs/order-management.md`. The reference output is the same directory layout as the prior alpha.106 run (8 csproj × Domain/Application/AntiCorruptionLayer/Api, each with src+tests).

## Summary

The framework absolutely produces a usable Clean Architecture service end-to-end from the spec + Copilot instructions, at the same shape and quality as the previous lab run. **No drift was severe enough to block the build or change the resulting architecture.** Friction was concentrated in three areas: (1) per-alpha API renames that the `dotnet new trellis-asp` template still references at the older version (alpha.337); (2) one analyzer (TRLS010) that requires a non-obvious refactor of state-machine code that combines a state transition with side-effecting domain operations; (3) one missing template-default that makes the canonical "default admin actor" sample broken out-of-the-box.

None of these prevented the lab from completing. They cost time inside the AI run (and would cost a human developer the same time on first contact). All three have low-effort fixes either in the framework, the template, or both.

---

## A. AspTemplate is two minor alphas behind framework HEAD

**Symptom.** `dotnet new trellis-asp` scaffolds `Directory.Packages.props` with `<TrellisVersion>3.0.0-alpha.337</TrellisVersion>`. Several alpha.360 features the spec wants to showcase are not in alpha.337:

- `IAuthorizedResource<TMessage, TResource>` (v4 typed accessor that lets the Cancel handler skip a duplicate Order load) was introduced after alpha.337.
- `Error.Conflict` constructor changed from `(ResourceRef?)` to `(ResourceRef?, string ReasonCode)`.
- `Trellis.FluentValidation.AddTrellisFluentValidation` was split into `Trellis.Mediator.FluentValidation` (separate NuGet, separate namespace).

The AI emitted code that targeted the *documented* alpha.360 surface (because the Copilot instructions pull `trellis-api-*.md` from alpha.360 NuGet, not from the scaffolded version pin), so the project failed to build until I bumped `TrellisVersion`. Symptoms on first compile:

```
CS0246: 'IAuthorizedResource<,>' could not be found
CS7036: required parameter 'ReasonCode' of 'Error.Conflict.Conflict(ResourceRef?, string)'
CS1061: 'IServiceCollection' does not contain a definition for 'AddTrellisFluentValidation'
```

**Fix scope** — template owner: pin scaffold to "newest stable alpha at template publish time" and update on Trellis releases. Either a `dotnet new trellis-asp --trellis-version 3.0.0-alpha.360` opt-in or a CI bump per Trellis release would resolve this.

**Workaround in this run.** Bumped `<TrellisVersion>` to 3.0.0-alpha.360 and added `<PackageVersion Include="Trellis.Mediator.FluentValidation" />`, plus `<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />` to satisfy the transitive constraint from `Trellis.Testing.AspNetCore` alpha.360.

---

## B. TRLS010 forbids throws inside `.Tap()` — requires `Bind` refactor for state-transition + side-effect orchestration

**Symptom.** The Order aggregate's `Submit(...)` and `Cancel(...)` methods compose a state-machine transition (`_machine.FireResult(trigger)`) with side-effecting domain operations (`product.ReserveStock(...)` / `product.ReleaseStock(...)`). Each side-effect returns `Result<StockQuantity>` and must be checked. The natural shape is:

```csharp
return _machine.FireResult(Triggers.Submit)
    .Tap(_ =>
    {
        foreach (var (product, qty) in reservations)
        {
            var r = product.ReserveStock(qty);
            if (r.IsFailure)
                throw new InvalidOperationException(/* invariant: preflight should have caught this */);
        }
        DomainEvents.Add(new OrderSubmittedEvent(...));
    });
```

That trips TRLS010 *"Don't throw exceptions inside 'Tap'. Return a failure Result instead to maintain Railway Oriented Programming semantics"*. Discarding with `_ = r;` instead of `if (r.IsFailure) throw` trips TRLS001 *"Result is not handled"*. The Tap signature `(T -> void)` does not let me return a failure from inside.

**Resolution shape that works** — promote to `Bind` so the lambda returns `Result<T>`:

```csharp
return _machine.FireResult(Triggers.Submit)
    .Bind(status =>
    {
        foreach (var (product, qty) in reservations)
        {
            var r = product.ReserveStock(qty);
            if (r.IsFailure) return Result.Fail<OrderStatus>(r.Error!);
        }
        return Result.Ok(status);
    })
    .Tap(_ => DomainEvents.Add(new OrderSubmittedEvent(...)));
```

That is the *correct* idiom — invariant violations should be failure Results, not exceptions. But TRLS010 emits today only when the `throw` is literally inside a `Tap` lambda. The diagnostic could go further:

**Suggested framework improvement.** Either:
1. Add a TRLS010-adjacent diagnostic that surfaces the `Bind`-vs-`Tap` choice when a `.Tap()` lambda contains a `Result`-returning call whose Result is discarded (today TRLS001 fires, but the suggested fix in the message is "handle the Result"; for Tap specifically the message should suggest "promote to Bind"); OR
2. Add a cookbook recipe shape "state transition + cascading side-effecting Results" since this is a common aggregate-method shape that I had to discover from scratch.

The Cancel method has the exact same shape (state transition + `product.ReleaseStock`). Both got the same Bind refactor.

---

## C. `DevelopmentActorProvider`'s default actor has empty permissions

**Symptom.** Spec §5.5 specifies: *"For testing and evaluation, the API layer reads a custom `X-Test-Actor` header... If the header is absent, use a default Admin actor so existing tests don't break."*

`AddDevelopmentActorProvider()` (no arg) defaults `DefaultPermissions` to an empty `HashSet<string>`. That means every test that does not bother to set X-Test-Actor gets a 403 on the first command. My API integration tests started failing with `authorization.insufficient.permissions` until I explicitly configured all 11 OM permissions on the default actor:

```csharp
services.AddDevelopmentActorProvider(options =>
{
    options.DefaultActorId = "development-admin";
    options.DefaultPermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.CustomersCreate, Permissions.ProductsCreate, /* + 9 more */
    };
});
```

This is technically correct (spec defines the permissions, so the service has to declare them). But it puts a footgun under every consumer:
- A consumer that runs the scaffold and hits the API with a browser gets blanket 403, with no in-band hint that the actor provider needs DefaultPermissions configured.
- The Copilot instructions don't mention this — `DevelopmentActorProvider`'s doc says *"a configurable default actor"* without flagging that the configurable bit is mandatory for any non-trivial flow.

**Suggested framework improvement.** Either:
1. Document this prominently in `trellis-api-asp.md` under `DevelopmentActorProvider` — emphasize that DefaultPermissions is empty by default and that this is the source of the first 403 surprise; OR
2. Add a `services.AddDevelopmentActorProvider(grantAllRegisteredPermissions: true)` overload that auto-discovers permissions from all registered `IAuthorize.RequiredPermissions` and grants them to the default actor (this would be opt-in only and Development-environment-gated; it would make the "scaffold and run" first impression Just Work without any consumer change for any future spec).

---

## D. Smaller frictions worth fixing

These cost 30–60 seconds each but accumulate:

1. **`StockQuantity` needs `[AllowZero]`.** `RequiredInt<T>` rejects 0 by default. The OM spec and almost every "stock quantity" domain naturally starts at 0. The Product ctor failed at runtime with `StockQuantity.TryCreate(0) must succeed — 0 is a valid stock quantity.` First-pass discovery is "I wrote a `ValidateAdditional` that allows 0; why does the framework still reject?" Answer: `RequiredInt`'s strict default is independent of `ValidateAdditional` — you have to additionally apply `[AllowZero]`. The Copilot instructions could call this out in the value-object taxonomy or anti-patterns file. It cost two test-build cycles to discover.

2. **`AddResourceAuthorization<TMessage,TResource,TResponse>()` does not register the IIdentifyResource bridge.** The doc says (correctly) *"Explicit AddResourceAuthorization<TMessage,TResource,TResponse>() inserts the behavior only; it does not automatically register the shared-loader bridge."* — but I missed it on first read and assumed the typed overload was a drop-in replacement for the assembly-scan overload. Symptoms were opaque: the handler's `_authorizedOrder.GetRequiredResource()` would throw and get wrapped into `Error.Unexpected`, with no Cause/StackTrace surfaced through the Result (see §E below). The recipe should make it loud: maybe a startup-time `InvalidOperationException` *"AddResourceAuthorization<T,U,V> was called but CancelOrderCommand implements IIdentifyResource<Order,OrderId> with no SharedResourceLoaderById<Order,OrderId> registered — call the assembly-scan overload or register the bridge manually"* when an `IIdentifyResource` message is registered with the typed overload only.

3. **`Error.Unexpected.Cause` is invisible in tests.** The framework wraps all unhandled exceptions thrown inside the pipeline into `Error.Unexpected("unhandled_exception")`. The `Cause` field is `null` in the rendered `ToString()` and the original exception's stack trace is dropped before the Result returns. In a test, the only feedback is `Cause = , ReasonCode = unhandled_exception` — which is intentional client-safe behavior but actively *anti*-helpful for tests where I'm the developer and I want the full trace. Suggested fix: an opt-in `services.AddTrellisBehaviors(options => options.PreserveCauseInResult = true)` for test/development environments, OR a `[LoggerMessage]` event at `LogLevel.Error` carrying the FaultId + full exception that a test can subscribe to via `MartinCostello.Logging.XUnit`.

4. **`Aggregate<T>.DomainEvents` is `protected`, not exposed for tests.** The cookbook says use `IAggregate.UncommittedEvents()` instead — which is correct but not the obvious first thing to reach for. A two-line note in `trellis-api-core.md` near the `Aggregate<TId>` docs would prevent the "why is DomainEvents inaccessible from my test" round-trip.

5. **OwnsOne's `addr.Property(a => a.Foo)` lambda parameter naming conflicts cascade.** When I named the ShippingAddress component `StateRegion` (because `State` reads weird) but EF's owned-property `addr.Property(a => a.StateRegion)` referred to the underlying `StateRegion` *type* not the property *name* `State`, the type inference for the OwnedNavigationBuilder cascaded into 5 CS1061 errors all blaming `Customer.Property`. The actual cause was one wrong property name on line 24. Not a Trellis bug — pure EF inference issue — but worth a cookbook note that owned-collection property selectors should match the property NAME, not the property TYPE.

---

## E. What worked well (signal-to-noise context)

To not bury the win: most of the surface delivers what the spec asks for, the first time:

- **`Aggregate<TId>` + `Entity<TId>` + `RequiredGuid<TSelf>` + state machine + ValueObject** — the whole DDD primitives surface composed cleanly. I never had to fight inheritance shape or constructor visibility.
- **Trellis.Primitives concrete value objects** (EmailAddress, PhoneNumber, Street/City/PostalCode/Country) — picked them up directly from the catalog with no surprises.
- **`IAuthorize.RequiredPermissions` + `services.AddResourceAuthorization(assembly)`** — the static-permission + resource-authorization composition Just Worked once the registration overload was correct.
- **Trellis.Asp `ToHttpResponseAsync(...)` / `AsActionResultAsync<T>`** — RFC 9457 ProblemDetails, 201 Created with Location, 403/404/409 status mapping all derived from the `Result<T>` Error type without controller branches.
- **Trellis.EntityFrameworkCore** — `ApplyTrellisConventions` auto-handled every value-object converter and the `Maybe<T>` properties on Order. The single config-class-per-aggregate pattern was clean.
- **Trellis.Testing `FakeRepository<T,TId>` + `TestActorProvider`** — set up a Mediator+behaviors test in 30 lines.
- **xUnit v3 `TestContext.Current.CancellationToken`** — caught me out at first but is the right shape long-term.

Final score from the AI's perspective: I would happily reach for this stack again. The frictions above are real but they're 5-minute fixes that accumulated; the structural shape is sound.

---

## F. Run metadata

- **Spec:** `specs/order-management.md` (382 lines, unchanged from prior runs)
- **Template:** Trellis.AspTemplate 1.0.19-alpha (`dotnet new trellis-asp`)
- **Framework:** Trellis 3.0.0-alpha.360 (bumped from scaffold's alpha.337)
- **Model:** Claude Opus 4.7 (1M context, internal)
- **Date:** 2026-06-08
- **Final test count:** `dotnet test -c Release` → 45 passed / 0 failed across 4 test projects (Domain.Tests 31, Application.Tests 6, AntiCorruptionLayer.Tests 3, Api.Tests 5)
- **Final build state:** 0 warnings, 0 errors across 4 src + 4 test csprojs, with `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` from the scaffold's Directory.Build.props.
