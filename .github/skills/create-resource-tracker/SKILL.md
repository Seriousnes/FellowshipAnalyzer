---
name: create-resource-tracker
description: "Create a ResourceTracker subclass that tracks a numeric resource (orbs, mana, charges, energy). Use when: adding resource generation/spending tracking, implementing a new resource type analyzer."
---

# Create ResourceTracker

A ResourceTracker is a specialized analyzer that tracks a numeric resource (orbs, mana, charges, energy) by subscribing to `ResourceChangeEvent` and `CastEvent`. It extends `ResourceTracker` instead of `Analyzer`.

## Procedure

### 1. Define resource constants

In the hero's analysis definition file (`{Hero}AnalysisDefinition.cs`), add constants:

```csharp
public const int {Resource}ResourceTypeId = 100; // Match the ID from combat log data
public const int Max{Resource} = 5;               // Maximum resource cap
```

### 2. Create the tracker class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Analyzers/{Resource}Tracker.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.{Hero}.Analysis;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analyzers;

public sealed class {Resource}Tracker : ResourceTracker
{
    public override void Initialize()
    {
        ResourceTypeId = {Hero}AnalysisDefinition.{Resource}ResourceTypeId;
        MaxResource = {Hero}AnalysisDefinition.Max{Resource};
        InitialResource = 0;

        base.Initialize(); // subscribes to ResourceChange + Cast events
    }
}
```

`base.Initialize()` automatically subscribes to:
- `Events.ResourceChange.By(SELECTED_PLAYER)` — tracks generation and waste
- `Events.Cast.By(SELECTED_PLAYER)` — tracks spending via `ClassResources` on cast events

### 3. Register on the CombatLogParser

```csharp
[AddModule<SpellUsable>]
[AddModule<{Resource}Tracker>]   // ← Add here, before analyzers that depend on it
[AddModule<Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

Place resource trackers **before** analyzers that depend on them (declaration order = priority order).

## What ResourceTracker Provides

| Property | Type | Description |
|----------|------|-------------|
| `Generated` | `int` | Total resource gained |
| `Wasted` | `int` | Total resource overcapped |
| `Spent` | `int` | Total resource consumed |
| `Current` | `int` | Current resource value |
| `GeneratorCastCounts` | `IReadOnlyDictionary<int, int>` | Cast count per generator ability |
| `SpenderCastCounts` | `IReadOnlyDictionary<int, int>` | Cast count per spender ability |
| `ResourceEvents` | `IReadOnlyList<ResourceEvent>` | Full timeline of gains/spends |

## Using Tracker Data in Other Analyzers

Other analyzers can depend on the tracker via constructor injection:

```csharp
public sealed class SpenderAnalyzer(CombatLogParser parser, {Resource}Tracker tracker) : Analyzer(parser)
{
    public override void Initialize()
    {
        // Use tracker data during analysis
    }

    public override void Complete()
    {
        var efficiency = 1.0 - (double)tracker.Wasted / (tracker.Generated + tracker.Wasted);
    }
}
```

## Key Rules

- Extends `ResourceTracker`, not `Analyzer`
- Must call `base.Initialize()` after setting `ResourceTypeId`, `MaxResource`, and `InitialResource`
- File goes in `Analyzers/` folder with naming convention `{Resource}Tracker.cs`
- Resource constants go in `{Hero}AnalysisDefinition`
- Register before dependent analyzers (priority order)
- The `ResourceTypeId` must match the `ResourceChangeType` field in `ResourceChangeEvent` from the combat log

## Checklist

- [ ] Resource constants defined in `{Hero}AnalysisDefinition`
- [ ] File is at `Analyzers/{Resource}Tracker.cs`
- [ ] Extends `ResourceTracker`
- [ ] Sets `ResourceTypeId`, `MaxResource`, `InitialResource` before `base.Initialize()`
- [ ] `[AddModule<T>]` on parser, ordered before dependent analyzers
