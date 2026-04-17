---
name: create-normalizer
description: "Create an IEventNormalizer that pre-processes combat log events before dispatch. Use when: reordering events, linking related events, fabricating synthetic events, or fixing event data before analyzers see it."
---

# Create Normalizer

A normalizer is a standalone class that implements `IEventNormalizer`. It runs **before** event dispatch, transforming the event list (reordering, linking, fabricating, filtering). Normalizers are not modules — they have no event subscriptions or lifecycle.

## Procedure

### 1. Create the normalizer class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Normalizers/{Name}Normalizer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.{Hero}.Normalizers;

public sealed class {Name}Normalizer : IEventNormalizer
{
    public int Priority => 0; // Lower runs first

    public IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId)
    {
        var result = new List<Event>(events.Count);

        foreach (var e in events)
        {
            // Transform, reorder, filter, or fabricate events
            result.Add(e);
        }

        return result;
    }
}
```

### 2. Register on the CombatLogParser

Add `[AddNormalizer<{Name}Normalizer>]` to the hero's parser:

```csharp
[AddNormalizer<{Name}Normalizer>]
[AddModule<SpellUsable>]
[AddModule<WinterOrbTracker>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

The source generator adds the normalizer to `RegisteredNormalizerTypes` and DI registration.

## Common Normalizer Patterns

### Event Reordering
Fix events that arrive out of logical order:
```csharp
public IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId)
{
    var result = events.ToList();
    // Move buff applications before the cast that triggered them
    // when they share the same timestamp
    result.Sort((a, b) => /* custom ordering logic */);
    return result;
}
```

### Event Linking
Associate related events (e.g., link a damage event to the cast that caused it):
```csharp
public IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId)
{
    // Build lookup of casts, then link subsequent damage/heal events
    return events;
}
```

### Event Fabrication
Synthesize events that the combat log doesn't emit directly:
```csharp
public IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId)
{
    var result = new List<Event>(events.Count);
    foreach (var e in events)
    {
        result.Add(e);
        if (e is CastEvent cast && ShouldFabricateSpend(cast))
        {
            result.Add(new SpendResourceEvent { Fabricated = true, Trigger = cast, /* ... */ });
        }
    }
    return result;
}
```

## IEventNormalizer Interface

```csharp
public interface IEventNormalizer
{
    int Priority { get; }
    IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId);
}
```

Normalizers run in `Priority` order (ascending) before any module initialization or event dispatch.

## Key Rules

- Normalizers are **not** modules — they don't extend `Module`, have no `Initialize()`/`Complete()`, no event subscriptions
- Registered via `[AddNormalizer<T>]`, not `[AddModule<T>]`
- File goes in `Normalizers/` folder
- Must be pure transformations — read the input list, return a new/modified list
- `Priority` controls execution order among normalizers (lower runs first)
- Normalizers are resolved from DI, so they can take constructor dependencies if needed

## Checklist

- [ ] File is at `Normalizers/{Name}Normalizer.cs`
- [ ] Implements `IEventNormalizer`
- [ ] `Priority` is set appropriately relative to other normalizers
- [ ] `[AddNormalizer<T>]` on the hero's CombatLogParser
- [ ] Returns a complete event list (doesn't accidentally drop events)
