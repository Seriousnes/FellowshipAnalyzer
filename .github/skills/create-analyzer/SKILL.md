---
name: create-analyzer
description: "Create a pure C# analyzer module that subscribes to combat log events, tracks state, and computes metrics. Use when: adding a new talent analyzer, ability analyzer, feature analyzer, or any event-driven analysis module. NOT for ResourceTrackers, guide components, or statistics components."
---

# Create Analyzer

An analyzer is a **pure C# class** in the `Analyzers/` folder that subscribes to combat log events, tracks state, and exposes computed metrics. It has no Blazor dependency. Guide and statistics rendering are separate files handled by the `create-guide` and `create-statistics` skills.

## Procedure

### 1. Create the analyzer class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Analyzers/{Name}Analyzer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.{Hero}.Combat;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analyzers;

public sealed class {Name}Analyzer(CombatLogParser parser) : Analyzer(parser)
{
    // --- Internal state ---
    private readonly List<SomeWindow> _windows = [];

    // --- Public accessors (consumed by guide/statistics components) ---
    public IReadOnlyList<SomeWindow> Windows => _windows;
    public int GoodCount => _windows.Count(w => w.IsGood);
    public int BadCount => _windows.Count(w => !w.IsGood);

    public override void Initialize()
    {
        AddEventListener(Events.ApplyBuff.By(SELECTED_PLAYER).Spell({Hero}Spells.SomeBuff.Id), OnBuffApply);
        AddEventListener(Events.RemoveBuff.By(SELECTED_PLAYER).Spell({Hero}Spells.SomeBuff.Id), OnBuffRemove);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
    }

    private void OnBuffApply(ApplyBuffEvent e)
    {
        _windows.Add(new SomeWindow(e.Timestamp));
    }

    private void OnBuffRemove(RemoveBuffEvent e)
    {
        if (_windows.Count > 0)
            _windows[^1] = _windows[^1] with { EndTimestamp = e.Timestamp };
    }

    private void OnCast(CastEvent e)
    {
        if (_windows.Count > 0 && _windows[^1].EndTimestamp is null)
            _windows[^1].Casts.Add(e);
    }
}
```

### 2. Register on the CombatLogParser

Add `[AddModule<{Name}Analyzer>]` to the hero's parser. Order matters — modules initialize in declaration order.

```csharp
[AddModule<TrackedStateModule>]
[AddModule<WinterOrbTracker>]
[AddModule<Abilities>]
[AddModule<{Name}Analyzer>]      // ← Add here
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

The source generator produces:
- A typed nullable property: `{Name}Analyzer? {Name}` (strips "Analyzer" suffix)
- DI registration in `Add{Hero}Analysis()`
- Assignment in `AssignModule()` switch

### 3. Optionally set StatisticsComponentType

If this analyzer will have a statistics component (created via the `create-statistics` skill), declare it:

```csharp
public override Type? StatisticsComponentType => typeof({Name}Statistics);
```

## Event Filter API

```csharp
// Static factory (Events.cs)
Events.Cast                    // EventFilter<CastEvent>
Events.ApplyBuff               // EventFilter<ApplyBuffEvent>
Events.RemoveBuff              // EventFilter<RemoveBuffEvent>
Events.Damage                  // EventFilter<DamageEvent>
Events.Heal                    // EventFilter<HealEvent>
Events.ResourceChange          // EventFilter<ResourceChangeEvent>
Events.Any                     // AnyEventFilter (matches all)

// Fluent filters
.By(SELECTED_PLAYER)           // sourceId == playerId
.By(SELECTED_PLAYER_PET)       // sourceId == player's pet
.To(SELECTED_PLAYER)           // targetId == playerId
.Spell(spellId)                // abilityGameId matches
.Spell(id1, id2, id3)          // abilityGameId matches any
```

## Dependencies

- **Required**: Constructor injection. DI resolves automatically.
  ```csharp
  public class MyAnalyzer(CombatLogParser parser, WinterOrbTracker tracker) : Analyzer(parser)
  ```
- **Optional**: Access via the parser's source-generated nullable properties.
  ```csharp
  var feralSpirit = Owner is RimeCombatLogParser rp ? rp.FeralSpirit : null;
  ```

## Naming Conventions

| Class Name | Generated Property |
|------------|--------------------|
| `BasicStComboAnalyzer` | `BasicStCombo` |
| `FreezingTorrentAnalyzer` | `FreezingTorrent` |
| `Abilities` | `Abilities` |

The source generator strips the "Analyzer" suffix from the class name.

## Key Rules

- Extends `Analyzer` (for resources, use the `create-resource-tracker` skill instead)
- Event subscriptions go in `Initialize()`, never the constructor
- Uses primary constructor with `CombatLogParser` (plus any required dependencies)
- Expose state via public read-only accessors — guide/statistics components consume these
- Pure C#: no `@using Microsoft.AspNetCore.Components`, no Razor, no `RenderFragment`
- File goes in `Analyzers/` folder

## Checklist

- [ ] File is at `Analyzers/{Name}Analyzer.cs`, pure C# with no Blazor references
- [ ] Extends `Analyzer` with primary constructor taking `CombatLogParser`
- [ ] Event subscriptions are in `Initialize()`
- [ ] Public accessors expose computed state for consumers
- [ ] `[AddModule<T>]` added to the hero's CombatLogParser in correct priority order
- [ ] `StatisticsComponentType` set if a statistics component exists
