---
name: create-resource-tracker
description: "Create a ResourceTracker subclass that tracks a numeric resource (orbs, mana, charges, energy). Use when: adding resource generation/spending tracking, implementing a new resource type analyzer."
---

# Create ResourceTracker

A resource tracker is a specialized module in `Modules/` that extends `ResourceTracker`. The base tracker tracks all observed `ResourceTypes` for the selected player and stores a `ResourceState` per resource type.

Use a hero-specific subclass when you need convenience accessors, max overrides, spell-definition cost lookup, or a statistics component for a specific resource.

## Procedure

### 1. Confirm The Resource Type

Resource IDs are represented by `FellowshipAnalyzer.Core.Game.ResourceTypes` in `src/FellowshipAnalyzer.Core/Game/ResourceTypes.cs`.

If the resource is missing from the enum, inspect a real log with the `analyze-log-resources` skill before adding a new enum value.

### 2. Create The Tracker Class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Resource}Tracker.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.{Hero}.Statistics;

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

public sealed class {Resource}Tracker : ResourceTracker
{
    public override Type? StatisticsComponentType => typeof({Resource}Statistics);

    public override void Initialize()
    {
        MaxOverrides[ResourceTypes.{Resource}] = {ResourceCap};
        base.Initialize();
    }

    protected override int? GetResourceCost(CastEvent castEvent, ResourceTypes type)
    {
        if (type != ResourceTypes.{Resource})
        {
            return null;
        }

        var spell = SpellRegistry.MaybeGet(castEvent.Ability.Guid) as I{Hero}Spell;
        return spell?.{Resource}Cost;
    }

    public ResourceState? {Resource}State => GetResourceState(ResourceTypes.{Resource});

    public int Generated => {Resource}State?.Generated ?? 0;
    public int Wasted => {Resource}State?.Wasted ?? 0;
    public int Spent => {Resource}State?.Spent ?? 0;
    public int Current => {Resource}State?.Current ?? 0;
}
```

Only set `MaxOverrides` when the resource cap cannot be trusted from event snapshots or should be fixed for the hero.

### 3. Register On The CombatLogParser

Register the tracker before analyzers that depend on it:

```csharp
[HeroAnalyzer("{hero-id}")]
[AddModule<{Resource}Tracker>]
[AddModule<Modules.Abilities>]
[AddModule<SomeAnalyzer>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

Declaration order is module priority.

## What ResourceTracker Provides

| API | Description |
| --- | --- |
| `GetResourceState(ResourceTypes type)` | Returns the tracked state or null if that resource never appeared. |
| `GetCurrent(type)` / `GetMax(type)` | Current and max values for a resource type. |
| `GetGenerated(type)` / `GetWasted(type)` / `GetSpent(type)` | Aggregate resource totals. |
| `GetGeneratorCasts(type)` / `GetSpenderCasts(type)` | Cast counts by ability ID. |
| `GetResourceEvents(type)` | Timeline for one resource type. |
| `AllResourceEvents` | Combined timeline across all resource types. |
| `CurrentHealth` / `MaxHealth` | Most recently observed selected-player health. |

The base tracker subscribes to:

- `Events.Any` to inspect selected-player `SourceResources` and `TargetResources` snapshots and fabricate `ResourceChangeEvent` gains.
- `Events.Cast.By(SELECTED_PLAYER)` to record spends from `ClassResource.Cost` or `GetResourceCost`.

## Using Tracker Data In Other Analyzers

Use `Owner.GetModule<T>()` in `Complete()` or after initialization:

```csharp
public sealed class SpenderAnalyzer : Analyzer
{
    public double Efficiency { get; private set; }

    public override void Complete()
    {
        var tracker = Owner.GetModule<{Resource}Tracker>();
        if (tracker is null)
        {
            return;
        }

        var totalPotential = tracker.Generated + tracker.Wasted;
        Efficiency = totalPotential == 0 ? 1 : 1.0 - (double)tracker.Wasted / totalPotential;
    }
}
```

## Key Rules

- Extend `ResourceTracker`, not `Analyzer`.
- Place the file in `Modules/` with naming convention `{Resource}Tracker.cs`.
- Use `ResourceTypes`, not raw resource IDs, in analyzer code.
- Override `GetResourceCost` when the log does not directly provide spend cost information.
- Call `base.Initialize()` if you override `Initialize()`.
- Register before dependent analyzers.
- Use the `analyze-log-resources` skill before adding enum values or guessing resource behavior.

## Checklist

- [ ] Resource type exists in `ResourceTypes` or was verified from logs before adding.
- [ ] File is at `Modules/{Resource}Tracker.cs`.
- [ ] Class extends `ResourceTracker`.
- [ ] Optional `MaxOverrides` are set before `base.Initialize()`.
- [ ] `GetResourceCost` is implemented if spend costs must come from spell metadata.
- [ ] `[AddModule<T>]` is on the parser before dependent analyzers.