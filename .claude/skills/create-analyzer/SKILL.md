---
name: create-analyzer
description: "Create a pure C# analyzer module that subscribes to combat log events, tracks state, and computes metrics. Use when: adding a new talent analyzer, ability analyzer, feature analyzer, or any event-driven analysis module. NOT for ResourceTrackers, guide components, or statistics components."
---

# Create Analyzer

An analyzer is a pure C# module in the `Modules/` folder. It subscribes to combat log events, tracks state, and exposes computed metrics for guide and statistics components. It has no Blazor dependency.

Guide rendering belongs in the `create-guide` skill. Statistics rendering belongs in the `create-statistics` skill. Resource tracking belongs in the `create-resource-tracker` skill.

## Procedure

### 1. Create The Analyzer Class

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Name}Analyzer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

public sealed partial class {Name}Analyzer : Analyzer
{
    private readonly List<SomeWindow> _windows = [];

    public IReadOnlyList<SomeWindow> Windows => _windows;
    public int GoodCount => _windows.Count(window => window.IsGood);
    public int BadCount => _windows.Count(window => !window.IsGood);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = SpellIds.SomeBuff)]
    private void OnBuffApply(ApplyBuffEvent applyBuffEvent)
    {
        _windows.Add(new SomeWindow(applyBuffEvent.Timestamp));
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = SpellIds.SomeBuff)]
    private void OnBuffRemove(RemoveBuffEvent removeBuffEvent)
    {
        var openWindow = _windows.LastOrDefault(window => window.EndTimestamp is null);
        if (openWindow is not null)
        {
            openWindow.EndTimestamp = removeBuffEvent.Timestamp;
        }
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        var openWindow = _windows.LastOrDefault(window => window.EndTimestamp is null);
        if (openWindow is not null)
        {
            openWindow.Casts.Add(castEvent);
        }
    }
}
```

Mark the class `partial` so the `ModuleGenerator` can emit its event-subscription override and any lazy-module accessors. Use simple helper records/classes in the same file unless they are large or shared.

### 2. Register On The CombatLogParser

Add `[AddModule<{Name}Analyzer>]` to the hero parser. Declaration order is module priority.

```csharp
[HeroAnalyzer(HeroName.{Hero})]
[AddModule<WinterOrbTracker>]
[AddModule<Modules.Abilities>]
[AddModule<{Name}Analyzer>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

The source generator produces:

- A typed nullable property on the parser: `{Name}Analyzer? {Name}`. The `Analyzer` suffix is stripped.
- DI registration in `Add{Hero}Analysis()`.
- Inclusion in `GetModuleTypes()` in declaration order.

### 3. Optionally Set StatisticsComponentType

If this analyzer has a statistics component, expose it from the module:

```csharp
public override Type? StatisticsComponentType => typeof({Name}Statistics);
```

## Event Subscription API

Declare each handler with a `[On<TEvent>]` attribute on a private (or internal) instance method. The `ModuleGenerator` translates the attributes into a `RegisterAttributeSubscriptions` override with inlined predicates.

```csharp
[On<CastEvent>(By = Actor.Player)]
private void OnCast(CastEvent e) { … }

[On<ApplyBuffEvent>(To = Actor.Player, Spell = SpellIds.SomeBuff)]
private void OnBuffApply(ApplyBuffEvent e) { … }

[On<DamageEvent>(By = Actor.Player, Spells = new[] { SpellIds.A, SpellIds.B })]
private void OnDamage(DamageEvent e) { … }
```

Supported attribute arguments:

| Argument | Effect |
|---|---|
| `By = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` | restrict source actor (event must implement `IHasSourceEvent`) |
| `To = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` | restrict target actor (event must implement `IHasTargetEvent`) |
| `Spell = SpellIds.X` | single ability guid match (event must implement `IAbilityEvent`) |
| `Spells = new[] { … }` | any of several ability guids |
| `ExtraSpell = …` / `ExtraSpells = new[] { … }` | filter `IExtraAbilityEvent.ExtraAbility.Id` |

Use `[On<Event>]` for an unfiltered "any event" subscription. Use `[On<FightStartEvent>]` / `[On<FightEndEvent>]` to hook the fabricated fight-boundary events for setup/finalization work — the `FightBookendNormalizer` prepends/appends those events to every analysis run.

## Dependencies

Modules are resolved from DI, then the parser assigns `Owner`. Do not require `CombatLogParser` in an analyzer constructor.

For module-to-module access, prefer `Lazy<TOther>` constructor injection. The `ModuleGenerator` emits a cached `_camelCaseName` private accessor for every primary-ctor parameter of type `Lazy<TModule>`:

```csharp
public sealed partial class FreezingTorrentAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    // generator emits: private SpellUsable _spellUsable => field ??= spellUsable.Value;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (_spellUsable.IsAvailable(e.Ability.Id)) { … }
    }
}
```

`Lazy<T>` defers resolution to dispatch time, so two modules that reference each other can ctor-inject through `Lazy<>` without hitting the FA0013 cycle diagnostic. Plain (non-Lazy) module-to-module ctor injection is fine for acyclic dependencies. For ad-hoc lookups, use `Owner.GetModule<T>()`.

## Naming Conventions

| Class Name | Generated Property |
|------------|--------------------|
| `BasicStComboAnalyzer` | `BasicStCombo` |
| `FreezingTorrentAnalyzer` | `FreezingTorrent` |
| `Abilities` | `Abilities` |

The source generator strips the `Analyzer` suffix from generated parser properties.

## Final Projections

For finalized metrics that depend on the entire event stream (window evaluations, score cards, summary findings), expose a `public TReport ToReport()` method on the module. The parser source generator picks up `ToReport()` automatically and includes it in the hero's typed `…AnalysisResult` record. Compute lazily — `ToReport()` must be idempotent and re-invokable.

For mutable public properties that older callers read, delegate to the report: `public int GoodCount => ToReport().GoodCount;`.

## Key Rules

- Extend `Analyzer`. For resources, use `ResourceTracker` through the `create-resource-tracker` skill.
- Mark the class `partial`.
- Declare event subscriptions with `[On<TEvent>]` attributes, never in the constructor.
- Use `Lazy<TOther>` ctor injection to break dependency cycles. Do not take `CombatLogParser` in the constructor.
- Compute finalized metrics in `ToReport()` (idempotent), not in any post-dispatch hook — `Module.Complete()` no longer exists.
- Expose state through public read-only accessors for guide/statistics components.
- Keep the module pure C#: no Razor, `RenderFragment`, or Blazor component dependencies.
- Place the file in `Modules/`.

## Checklist

- [ ] File is at `Modules/{Name}Analyzer.cs`.
- [ ] Class is `partial` and extends `Analyzer`.
- [ ] Event handlers are decorated with `[On<TEvent>]` attributes.
- [ ] Cross-module reads use `Lazy<TOther>` ctor injection (or `Owner.GetModule<T>()`).
- [ ] Finalized projections live in `ToReport()`, not in a `Complete()` override.
- [ ] Public accessors expose computed state for consumers.
- [ ] `[AddModule<T>]` is added to the hero parser in the correct priority order.
- [ ] `StatisticsComponentType` is set if a statistics component exists.
