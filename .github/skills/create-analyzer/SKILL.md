---
name: create-analyzer
description: "Create a pure C# analyzer module that subscribes to combat log events, tracks state, and computes metrics. Use when: adding a new talent analyzer, ability analyzer, feature analyzer, or any event-driven analysis module. NOT for ResourceTrackers, guide components, or statistics components."
---

# Create Analyzer

An analyzer is a pure C# module in the `Modules/` folder. It subscribes to combat log events, tracks state, and exposes computed metrics for guide and statistics components. It has no Blazor dependency.

Guide rendering belongs in the `create-guide` skill. Statistics rendering belongs in the `create-statistics` skill. Resource tracking belongs in the `create-resource-tracker` skill.

## Procedure

### 1. Create The Analyzer Class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Name}Analyzer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

public sealed class {Name}Analyzer : Analyzer
{
    private readonly List<SomeWindow> _windows = [];

    public IReadOnlyList<SomeWindow> Windows => _windows;
    public int GoodCount => _windows.Count(window => window.IsGood);
    public int BadCount => _windows.Count(window => !window.IsGood);

    public override void Initialize()
    {
        AddEventListener(Events.ApplyBuff.By(SELECTED_PLAYER).Spell(Spells.SomeBuff), OnBuffApply);
        AddEventListener(Events.RemoveBuff.By(SELECTED_PLAYER).Spell(Spells.SomeBuff), OnBuffRemove);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
    }

    private void OnBuffApply(ApplyBuffEvent applyBuffEvent)
    {
        _windows.Add(new SomeWindow(applyBuffEvent.Timestamp));
    }

    private void OnBuffRemove(RemoveBuffEvent removeBuffEvent)
    {
        var openWindow = _windows.LastOrDefault(window => window.EndTimestamp is null);
        if (openWindow is not null)
        {
            openWindow.EndTimestamp = removeBuffEvent.Timestamp;
        }
    }

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

Use simple helper records/classes in the same file unless they are large or shared.

### 2. Register On The CombatLogParser

Add `[AddModule<{Name}Analyzer>]` to the hero parser. Declaration order is module priority.

```csharp
[HeroAnalyzer("{hero-id}")]
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

## Event Filter API

```csharp
Events.Cast                    // EventFilter<CastEvent>
Events.ApplyBuff               // EventFilter<ApplyBuffEvent>
Events.RemoveBuff              // EventFilter<RemoveBuffEvent>
Events.Damage                  // EventFilter<DamageEvent>
Events.Heal                    // EventFilter<HealEvent>
Events.ResourceChange          // EventFilter<ResourceChangeEvent>
Events.Any                     // AnyEventFilter, matches all events

.By(SELECTED_PLAYER)           // source matches analyzed player
.By(SELECTED_PLAYER_PET)       // source matches analyzed player's pet, when pet tracking exists
.To(SELECTED_PLAYER)           // target matches analyzed player
.Spell(spellA, spellB)         // ability id matches any Spell.Guid
.ExtraSpell(spellA)            // extra ability id matches any Spell.Guid
```

`Spell(...)` takes `Spell` or `Effect` instances from `FellowshipAnalyzer.Core.Common.Spells`, not raw IDs.

## Dependencies

Modules are resolved from DI, then the parser assigns `Owner`. Do not require `CombatLogParser` in an analyzer constructor.

For module-to-module access, use `Owner.GetModule<T>()` or the hero parser's generated properties:

```csharp
public override void Complete()
{
    var tracker = Owner.GetModule<WinterOrbTracker>();
    if (tracker is null)
    {
        return;
    }

    var generated = tracker.Generated;
}
```

Constructor injection is acceptable for ordinary DI services. If injecting another module, confirm it is registered by `[AddModule<T>]` and avoid using it before both modules have completed `Initialize()`.

## Naming Conventions

| Class Name | Generated Property |
|------------|--------------------|
| `BasicStComboAnalyzer` | `BasicStCombo` |
| `FreezingTorrentAnalyzer` | `FreezingTorrent` |
| `Abilities` | `Abilities` |

The source generator strips the `Analyzer` suffix from generated parser properties.

## Key Rules

- Extend `Analyzer`. For resources, use `ResourceTracker` through the `create-resource-tracker` skill.
- Put event subscriptions in `Initialize()`, never in the constructor.
- Keep final scoring, aggregations, and derived summaries in `Complete()` when they depend on the full event stream.
- Expose state through public read-only accessors for guide/statistics components.
- Keep the module pure C#: no Razor, `RenderFragment`, or Blazor component dependencies.
- Place the file in `Modules/`.

## Checklist

- [ ] File is at `Modules/{Name}Analyzer.cs`.
- [ ] Class extends `Analyzer` and does not require `CombatLogParser` in its constructor.
- [ ] Event subscriptions are in `Initialize()`.
- [ ] Public accessors expose computed state for consumers.
- [ ] `[AddModule<T>]` is added to the hero parser in the correct priority order.
- [ ] `StatisticsComponentType` is set if a statistics component exists.