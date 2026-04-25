---
name: create-normalizer
description: "Create an IEventNormalizer that pre-processes combat log events before dispatch. Use when: reordering events, linking related events, fabricating synthetic events, or fixing event data before analyzers see it."
---

# Create Normalizer

A normalizer is a standalone class that implements `IEventNormalizer`. It runs before module initialization and event dispatch, transforming the event list through hydration, scaling, reordering, linking, fabrication, or filtering.

Normalizers are not modules. They do not extend `Module`, have no `Initialize()` or `Complete()`, and do not subscribe to events.

## Procedure

### 1. Create The Normalizer Class

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Normalizers/{Name}Normalizer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.{Hero}.Normalizers;

public sealed class {Name}Normalizer : IEventNormalizer
{
    public int Priority => 0;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var result = new List<Event>(events.Count);

        foreach (var combatEvent in events)
        {
            result.Add(combatEvent);
        }

        return result;
    }
}
```

The current interface is:

```csharp
public interface IEventNormalizer
{
    int Priority { get; }
    List<Event> Normalize(List<Event> events, int playerId);
}
```

### 2. Register On The CombatLogParser

Add `[AddNormalizer<{Name}Normalizer>]` to the hero parser:

```csharp
[HeroAnalyzer("{hero-id}")]
[AddNormalizer<{Name}Normalizer>]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

The source generator emits `GetNormalizerTypes()` and DI registration for hero-specific normalizers.

Current execution follows the generated normalizer type list, which preserves `[AddNormalizer<T>]` declaration order with base parser normalizers before hero parser normalizers. Keep `Priority` consistent with the intended order because normalizer implementations use it as documentation.

## Common Normalizer Patterns

### Event Reordering

```csharp
public List<Event> Normalize(List<Event> events, int playerId)
{
    var result = events.ToList();
    result.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
    return result;
}
```

### Event Linking

```csharp
public List<Event> Normalize(List<Event> events, int playerId)
{
    var castsByTimestamp = events
        .OfType<CastEvent>()
        .ToLookup(castEvent => (castEvent.Timestamp, castEvent.SourceId, castEvent.Ability.Guid));

    foreach (var damageEvent in events.OfType<DamageEvent>())
    {
        var matchingCast = castsByTimestamp[(damageEvent.Timestamp, damageEvent.SourceId, damageEvent.Ability.Guid)]
            .FirstOrDefault();
        if (matchingCast is not null)
        {
            damageEvent.LinkedEvents.Add(new LinkedEvent(matchingCast, "matching-cast"));
        }
    }

    return events;
}
```

### Event Fabrication

```csharp
public List<Event> Normalize(List<Event> events, int playerId)
{
    var result = new List<Event>(events.Count);

    foreach (var combatEvent in events)
    {
        result.Add(combatEvent);

        if (combatEvent is CastEvent castEvent && ShouldFabricateSpend(castEvent))
        {
            result.Add(new SpendResourceEvent
            {
                Timestamp = castEvent.Timestamp,
                SourceId = castEvent.SourceId,
                TargetId = castEvent.TargetId,
                Ability = castEvent.Ability,
                Fabricated = true,
                Trigger = castEvent,
            });
        }
    }

    return result;
}
```

## Key Rules

- Normalizers are not modules.
- Register with `[AddNormalizer<T>]`, not `[AddModule<T>]`.
- Use the current `List<Event> Normalize(List<Event> events, int playerId)` signature.
- File goes in `Normalizers/`.
- Return a complete event list; do not accidentally drop events.
- Mutating the input list is allowed when intentional. Return a new list when reordering or filtering is clearer.
- Normalizers are resolved from DI, so constructor injection can be used for registered services.

## Checklist

- [ ] File is at `Normalizers/{Name}Normalizer.cs`.
- [ ] Implements `IEventNormalizer` with the current `List<Event>` signature.
- [ ] `[AddNormalizer<T>]` is on the hero parser.
- [ ] Declaration order and `Priority` agree with the intended order.
- [ ] Returns a complete event list.