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

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Resource}Tracker.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.{Hero}.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

public sealed partial class {Resource}Tracker : ResourceTracker
{
    public {Resource}Tracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        MaxOverrides[ResourceTypes.{Resource}] = {ResourceCap};
    }

    public override Type? StatisticsComponentType => typeof({Resource}Statistics);

    protected override int? GetResourceCost(CastEvent castEvent, ResourceTypes type)
    {
        if (type != ResourceTypes.{Resource})
        {
            return null;
        }

        var spell = SpellRegistry.MaybeGet(castEvent.Ability.FSLID) as I{Hero}Spell;
        return spell?.{Resource}Cost;
    }

    public ResourceState? {Resource}State => GetResourceState(ResourceTypes.{Resource});

    public int Generated => {Resource}State?.Generated ?? 0;
    public int Wasted => {Resource}State?.Wasted ?? 0;
    public int Spent => {Resource}State?.Spent ?? 0;
    public int Current => {Resource}State?.Current ?? 0;
}
```

Mark the class `partial` so the `ModuleGenerator` can emit the inherited `[On<>]` subscriptions. Only set `MaxOverrides` when the resource cap cannot be trusted from event snapshots or should be fixed for the hero.

### 3. Register On The CombatLogParser

Register the tracker before analyzers that depend on it:

```csharp
[HeroAnalyzer(HeroName.{Hero})]
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

The base tracker declares the following `[On<>]` subscriptions:

- `[On<Event>]` to inspect selected-player `SourceResources` and `TargetResources` snapshots.
- `[On<CastEvent>(By = Actor.Player)]` to record spends from `ClassResource.Cost` or `GetResourceCost`.
- `[On<ResourceChangeEvent>(By = Actor.Player)]` to record gains.

## Using Tracker Data In Other Analyzers

Inject the tracker via `Lazy<{Resource}Tracker>` on the consuming module and read it through the generator-emitted accessor:

```csharp
public sealed partial class SpenderAnalyzer(Lazy<{Resource}Tracker> tracker) : Analyzer
{
    public double Efficiency
    {
        get
        {
            var totalPotential = _tracker.Generated + _tracker.Wasted;
            return totalPotential == 0 ? 1 : 1.0 - (double)_tracker.Wasted / totalPotential;
        }
    }
}
```

For ad-hoc reads outside a ctor-injected scenario, `Owner.GetModule<T>()` also works.

## Key Rules

- Extend `ResourceTracker`, not `Analyzer`.
- Mark the class `partial`.
- Place the file in `Modules/` with naming convention `{Resource}Tracker.cs`.
- Use `ResourceTypes`, not raw resource IDs, in analyzer code.
- Override `GetResourceCost` when the log does not directly provide spend cost information.
- Set `MaxOverrides` in the constructor, not in any post-construction hook (`Module.Initialize` no longer exists).
- Register before dependent analyzers.
- Use the `analyze-log-resources` skill before adding enum values or guessing resource behavior.

## Checklist

- [ ] Resource type exists in `ResourceTypes` or was verified from logs before adding.
- [ ] File is at `Modules/{Resource}Tracker.cs`.
- [ ] Class is `partial` and extends `ResourceTracker`.
- [ ] Optional `MaxOverrides` are set in the constructor.
- [ ] `GetResourceCost` is implemented if spend costs must come from spell metadata.
- [ ] `[AddModule<T>]` is on the parser before dependent analyzers.
