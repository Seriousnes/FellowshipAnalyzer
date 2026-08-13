---
name: create-resource-tracker
description: "Create a ResourceTracker subclass that tracks a numeric resource (orbs, mana, charges, energy). Use when: adding resource generation/spending tracking, implementing a new resource type analyzer."
---

# Create ResourceTracker

A resource tracker is a specialized module in `Modules/` that extends `ResourceTracker`. The base tracker tracks all observed `ResourceTypes` for the selected player and stores a `ResourceState` per resource type.

`ResourceTracker` derives from `Analyzer` but is registered dungeon-lifetime, so its inherited `Pull` property is never assigned. Do not read `Pull` from a tracker; it accumulates across the whole dungeon.

Use a hero-specific subclass when you need convenience accessors, max overrides, spell-definition cost lookup, or a statistics component for a specific resource.

## Procedure

### 1. Confirm The Resource Type

Resource IDs are represented by `FellowshipAnalyzer.Core.Game.ResourceTypes` in `src/FellowshipAnalyzer.Core/Game/ResourceTypes.cs`. Each member carries `[ResourceName]` attributes mapping the in-game display names (Primary covers Anima, Energy, Fury, Cinders, Focus, and so on).

If the resource is missing from the enum, inspect a real log with the `analyze-log-resources` skill before adding a new enum value.

Note the namespaces despite the shared `Game/` folder: `ResourceTypes` is in `FellowshipAnalyzer.Core.Game`, while `ResourceTracker` and `ResourceState` are in `FellowshipAnalyzer.Core.Resources`.

### 2. Create The Tracker Class

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Resource}Tracker.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
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

    protected override int? GetResourceCost(CastEvent castEvent, ResourceTypes type) =>
        SpellRegistry.MaybeGet(castEvent.Ability.FSLID)?.Cost(type);

    public ResourceState? {Resource}State => GetResourceState(ResourceTypes.{Resource});

    public int Generated => {Resource}State?.Generated ?? 0;
    public int Wasted => {Resource}State?.Wasted ?? 0;
    public int Spent => {Resource}State?.Spent ?? 0;
    public int Current => {Resource}State?.Current ?? 0;
}
```

Mark the class `partial` so the `ModuleGenerator` can emit the inherited `[On<>]` subscriptions. Only set `MaxOverrides` when the resource cap cannot be trusted from event snapshots or should be fixed for the hero.

Costs are stored on `Spell` as a `ResourceTypes`-keyed dictionary and read with `Cost(ResourceTypes)`. The `I{Hero}Spell` interfaces (`WinterOrbCost`-style named accessors) are sugar over the same dictionary and exist only for Rime, Elarion and Ardeos; do not add a new one.

`ResourceTracker` keeps a hand-written constructor because it takes `ILogger<ResourceTracker>` from the service provider, which is exactly the case `[Uses<T>]` does not cover.

### 3. Register On The CombatLogParser

Register the tracker before analyzers that depend on it:

```csharp
[HeroAnalyzer(HeroName.{Hero})]
[AddAnalyzer<{Resource}Tracker>]
[AddModule<Modules.Abilities>]
[AddAnalyzer<SomeAnalyzer>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

A tracker subscribes to events, so it registers with `[AddAnalyzer<T>]` (FA0019). Declaring no `[ForPull]` is what keeps it dungeon-lifetime, which is what makes it readable from a pull analyzer: FA0014 reports a dependency on a `[ForPull]` type, whatever registered it.

Declaration order is the default module order (base parser modules first, then the hero's, in the order declared) and the tie-break for the sort. Use `[Before<TOther>]` or `[After<TOther>]` on a module when it must run relative to a specific other module rather than relying on where it sits in the attribute list.

## What ResourceTracker Provides

| API | Description |
| --- | --- |
| `GetResourceState(ResourceTypes type)` | Returns the tracked state or null if that resource never appeared. |
| `GetCurrent(type)` / `GetMax(type)` | Current and max values for a resource type. |
| `GetGenerated(type)` / `GetWasted(type)` / `GetSpent(type)` | Aggregate resource totals. |
| `GetGeneratorCasts(type)` / `GetSpenderCasts(type)` | Cast counts by ability ID. |
| `GetResourceEvents(type)` | Timeline for one resource type. |
| `AllResourceEvents` | Combined timeline across all resource types. |
| `CurrentHealth` / `MaxHealth` | Most recently observed selected-player health (`long`). |

The base tracker declares the following `[On<>]` subscriptions:

- `[On<Event>]` to inspect selected-player `SourceResources` and `TargetResources` snapshots.
- `[On<CastEvent>(By = Actor.Player)]` to record spends from `ClassResource.Cost` or `GetResourceCost`.
- `[On<ResourceChangeEvent>(By = Actor.Player)]` to record gains.

`ResourceNormalizer` divides every snapshot's Amount/Max/Cost by 100 before dispatch, so tracker values are in-game units (Winter Orbs 0-5, Cinders 0-400), never the raw log scale. The tracker appends a `ResourceEvent` only on positive deltas, so `GetResourceEvents` is gain-only; a faithful over-time series needs a custom `[On<Event>]` snapshot hook.

## Using Tracker Data In Other Analyzers

Declare the dependency with `[Uses<T>]` and read it through the generator-emitted PascalCase accessor:

```csharp
[Uses<{Resource}Tracker>]
public sealed partial class SpenderAnalyzer : Analyzer
{
    public double Efficiency
    {
        get
        {
            var totalPotential = {Resource}Tracker.Generated + {Resource}Tracker.Wasted;
            return totalPotential == 0 ? 1 : 1.0 - (double){Resource}Tracker.Wasted / totalPotential;
        }
    }
}
```

For ad-hoc reads outside a declared dependency, `Owner.GetModule<T>()` also works.

## Key Rules

- Extend `ResourceTracker`, not `Analyzer` directly.
- Mark the class `partial`.
- Place the file in `Modules/` with naming convention `{Resource}Tracker.cs`.
- Use `ResourceTypes`, not raw resource IDs, in analyzer code.
- Override `GetResourceCost` when the log does not directly provide spend cost information; read costs with `Spell.Cost(ResourceTypes)`.
- Set `MaxOverrides` in the constructor, not in any post-construction hook.
- Register with `[AddAnalyzer<T>]`, and no `[ForPull]`, before dependent analyzers.
- Never read `Pull` from a tracker; it is dungeon-lifetime.
- Use the `analyze-log-resources` skill before adding enum values or guessing resource behavior.

## Checklist

- [ ] Resource type exists in `ResourceTypes` or was verified from logs before adding.
- [ ] File is at `Modules/{Resource}Tracker.cs`.
- [ ] Class is `partial` and extends `ResourceTracker`.
- [ ] Optional `MaxOverrides` are set in the constructor.
- [ ] `GetResourceCost` is implemented if spend costs must come from spell metadata.
- [ ] `[AddAnalyzer<T>]`, with no `[ForPull]`, is on the parser before dependent analyzers.
