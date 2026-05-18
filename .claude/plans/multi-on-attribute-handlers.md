# Plan — Reliable multi-`[On<TEvent>]` handlers

## Context

`OnAttribute<TEvent>` is declared with `AllowMultiple = true` so a single handler method can stack several `[On<...>]` attributes (`src/FellowshipAnalyzer.Core/Analysis/OnAttribute.cs:10`). `ModuleGenerator` already iterates `method.GetAttributes()` and emits one `Subscribe(...)` call per attribute (`src/FellowshipAnalyzer.Generators/ModuleGenerator.cs:127-137`), so dispatch fan-out works in principle. Two failure modes block the desired ergonomics:

1. **Interface-typed handler parameters are silently dropped.** `BuildHandler` validates the parameter type with `InheritsFrom(eventType, paramNamed)` (`ModuleGenerator.cs:241-254`), which only walks `ITypeSymbol.BaseType`. Interfaces live on `AllInterfaces`, so a parameter typed as `IHasSourceEvent` (or any other `EventInterfaces.cs` marker) makes `BuildHandler` return `null` and no subscription is emitted. Meanwhile `OnHandlerSignatureAnalyzer.IsAssignableFrom` (`src/FellowshipAnalyzer.Analyzers/OnHandlerSignatureAnalyzer.cs:75-90`) **does** check `AllInterfaces` and considers the signature valid, so FA0011 never fires. The handler appears correctly authored, compiles cleanly, and never runs.

2. **`OneOf<T0,T1,T2>`-style parameters won't work.** Even if validation accepted the type, the emitter (`ModuleGenerator.cs:391-395`) hard-codes a direct cast `MethodName((CastEvent)e)`. No conversion path exists from `CastEvent` to `OneOf<CastEvent,…>`, and `OneOf` is not on the dependency list — adding it would land in `FellowshipAnalyzer.Core` and ship to every WASM client under AOT.

A third smaller problem is invisible today but worth fixing alongside: the analyzer and generator use **two different** definitions of "is the handler parameter compatible with the attribute's TEvent". As soon as either side changes (e.g. extending `InheritsFrom` here), they will drift again unless the rule is shared.

The desired surface:

```csharp
[On<CastEvent>, On<FreeCastEvent>, On<ApplyBuffEvent>]
public void OnCastOrBuff(/* common type */ evt) { … }

[On<CastEvent>]
[On<HealEvent>]
public void OnAnySourced(IHasSourceEvent sourced) { … }
```

## Critical files

| File | Role |
|---|---|
| `src/FellowshipAnalyzer.Generators/HandlerSignatureRules.cs` | **New.** Single source of truth: `IsCompatibleParam(ITypeSymbol param, INamedTypeSymbol eventType)`. Lives in the generators project; source-linked into the analyzer project. |
| `src/FellowshipAnalyzer.Generators/ModuleGenerator.cs` | Replace `InheritsFrom` param check (`ModuleGenerator.cs:252-253`) with `HandlerSignatureRules.IsCompatibleParam`. Cast emission stays as-is — `(CastEvent)e` already upcasts implicitly to any base/interface the handler param happens to be. |
| `src/FellowshipAnalyzer.Analyzers/OnHandlerSignatureAnalyzer.cs` | Delete the duplicate `IsAssignableFrom` (`OnHandlerSignatureAnalyzer.cs:75-90`); call the shared rule. Expand FA0011 message to name the specific attribute that fails when multiple are present. |
| `src/FellowshipAnalyzer.Analyzers/FellowshipAnalyzer.Analyzers.csproj` | Add `<Compile Include="..\FellowshipAnalyzer.Generators\HandlerSignatureRules.cs" Link="HandlerSignatureRules.cs" />`. Source-only sharing — analyzers can't take a runtime dep on the generator assembly. |
| `src/FellowshipAnalyzer.Analyzers/AnalyzerReleases.Unshipped.md` | Record the FA0011 message change (Roslyn convention). |
| `tests/FellowshipAnalyzer.Generators.Tests/MultiOnAttributeTests.cs` | **New.** Snapshot tests for the cases in the *Test matrix* table below. |
| `tests/FellowshipAnalyzer.Core.Tests/Analysis/MultiOnHandlerTests.cs` | **New.** Runtime test: a fixture `Module` declares `[On<CastEvent>, On<HealEvent>] void H(IAbilityEvent e) => _count++;`, drive an `EventEmitter` with one cast + one heal, assert `_count == 2`. Today this would assert 0. |

## Existing facilities to reuse

- `OnAttribute<TEvent>` already supports stacking (`OnAttribute.cs:10`, `AllowMultiple = true`).
- `ModuleGenerator.GetSubscriberInfo` already iterates all attributes per method and builds an independent `HandlerInfo` per attribute (`ModuleGenerator.cs:124-138`). The fan-out plumbing is correct — only per-attribute validation is wrong.
- `EventInterfaces.cs` already defines `IHasSourceEvent`, `IHasTargetEvent`, `IAbilityEvent`, `IExtraAbilityEvent`, `IAmountEvent` etc. These are the primary "common type" lever for multi-event handlers and need no changes.
- `OnHandlerSignatureAnalyzer` already iterates attributes (`OnHandlerSignatureAnalyzer.cs:42-54`) — sharing the rule means it reports the right attribute by name automatically.
- `FellowshipAnalyzer.Generators.Tests` project already exists (empty) — landing pad for snapshot tests.

## Shared compatibility rule

```csharp
internal static class HandlerSignatureRules
{
    public static bool IsCompatibleParam(ITypeSymbol param, INamedTypeSymbol eventType)
    {
        if (SymbolEqualityComparer.Default.Equals(param, eventType)) return true;
        for (var b = eventType.BaseType; b is not null; b = b.BaseType)
            if (SymbolEqualityComparer.Default.Equals(param, b)) return true;
        foreach (var i in eventType.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(param, i)) return true;
        return false;
    }
}
```

Compatibility behavior cannot drift again because both consumers go through this one method.

## Cast emission stays unchanged

The emitter unconditionally writes `(EventTypeFullyQualified)e` (`ModuleGenerator.cs:391-395`). This is correct for every accepted param shape:

- Param == `eventType`: exact match.
- Param is a base of `eventType`: `(CastEvent)e` then implicit upcast to `Event` / `BaseCastEvent`.
- Param is an interface of `eventType`: `(CastEvent)e` then implicit upcast to `IHasSourceEvent` etc.

Fixing validation is sufficient — no emitter changes.

## Multi-attribute behavior

Each `[On<X>]` is validated against the param independently. `[On<CastEvent>, On<ApplyBuffEvent>] void H(IAbilityEvent e)` passes both (both implement `IAbilityEvent`). `[On<CastEvent>, On<DeathEvent>] void H(IHasSourceEvent e)` fails the second (`DeathEvent` has no `IHasSourceEvent`); FA0011 reports on the specific attribute, message extended to name it:

> Handler 'OnX' marked with `[On<DeathEvent>]` must take a single parameter assignable from `DeathEvent` and return void/Task/ValueTask. Param type `IHasSourceEvent` is not implemented by `DeathEvent`.

## `OneOf<>` — explicitly out of scope

Adding a `OneOf` package costs WASM/AOT bundle size and a generator code path that emits per-attribute discriminator construction (`OneOf<…>.FromT0((CastEvent)e)`, `FromT1((HealEvent)e)`, …). The same expressive power is already available via:

- **Shared interface** (preferred). Multiple events implementing a common marker interface is the idiomatic shape for this codebase (`EventInterfaces.cs`). If two events conceptually share something but don't share an interface today, adding a marker interface is the same work as wiring a `OneOf` discriminator — but the marker is reusable everywhere else.
- **`Event` parameter + pattern match.** For genuinely heterogeneous unions, `[On<A>, On<B>] void H(Event e) { switch(e) { case A a: …; case B b: …; } }` is one line longer than `OneOf` and adds zero dependencies.

If a future case needs it, `OneOf` is a strict additive change (detect the param type, replace the cast site with discriminator construction). The test matrix below asserts current behavior with a row that flips to "passes" if/when that lands.

## Test matrix

| Case | Expectation |
|---|---|
| `[On<CastEvent>] void H(CastEvent e)` | Single Subscribe, predicate `e is CastEvent __e0`, delegate `(CastEvent)e`. Regression guard. |
| `[On<CastEvent>, On<HealEvent>] void H(IAbilityEvent e)` | Two Subscribe calls, distinct predicates, both delegate sites cast to the concrete event. |
| `[On<CastEvent>, On<ApplyBuffEvent>] void H(IHasSourceEvent e)` | Two Subscribe calls. Previously silently emitted nothing. |
| `[On<CastEvent>, On<DeathEvent>] void H(IHasSourceEvent e)` | One Subscribe (`CastEvent` only); FA0011 reported on the `[On<DeathEvent>]` attribute. |
| `[On<CastEvent>] void H(IHasSourceEvent e)` | Single Subscribe. Generator and analyzer both accept. |
| `[On<CastEvent>] void H(Event e)` | Single Subscribe. Existing path — regression guard. |
| `[On<CastEvent>] void H(OneOf<CastEvent,HealEvent> e)` | FA0011 reported. Documents the deferred decision; flips to "passes" if `OneOf` support lands later. |

## Risks

- **Behavior change for existing modules.** Today, any module that accidentally typed a handler parameter as an interface compiled cleanly and silently never fired. After this fix those handlers start firing. Audit before merging: grep `src/` for `\[On<.*>\]` followed by a handler whose parameter type is one of the `EventInterfaces.cs` interfaces. Expect zero matches — the silent failure mode means anyone who tried this moved away from it. If matches exist, review each as a behavior change.
- **Source-link friction between analyzer and generator.** The analyzer project source-links `HandlerSignatureRules.cs` rather than taking a project reference. Roslyn analyzers can't depend on the generator assembly (different load context). If the file grows beyond compatibility helpers, revisit.
- **FA0011 message change is observable.** Test fixtures pinned on the exact diagnostic message will break — update alongside.
- **Diagnostic noise for partial regressions.** A user updating one attribute on a multi-`[On<>]` method may temporarily have one valid and one invalid attribute. FA0011 reporting per-attribute is the right granularity but means two squigglies on the same line for two-bad-out-of-three cases.
- **`Event` as a handler param is legal everywhere.** Already legal today via the base-class path in `InheritsFrom`; the shared rule just makes it explicit. Lint-worthy if it becomes a pattern, not blocking.

## Verification

- `dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal` — succeeds with no new warnings.
- `dotnet test FellowshipAnalyzer.slnx --no-build` — full suite green.
- Targeted: `dotnet test tests/FellowshipAnalyzer.Generators.Tests/FellowshipAnalyzer.Generators.Tests.csproj --no-build` and `dotnet test tests/FellowshipAnalyzer.Core.Tests/FellowshipAnalyzer.Core.Tests.csproj --no-build --filter "FullyQualifiedName~MultiOnHandler"`.
- Spot-check a `[On<CastEvent>] void H(IBogusInterface e)` (where `CastEvent` does not implement `IBogusInterface`) and confirm FA0011 reports with the new message naming `[On<CastEvent>]`.
- Open a Rime report end-to-end via Aspire and confirm no analyzer numbers shift versus a baseline (fix is additive; no existing handler uses an interface param today).
