---
name: create-normalizer
description: "Create an IEventNormalizer that pre-processes combat log events before dispatch. Use when: reordering events, linking related events, fabricating synthetic events, or fixing event data before analyzers see it."
---

# Create Normalizer

A normalizer is a standalone class that implements `IEventNormalizer`. It runs after the parser has constructed its modules but before any subscriptions are registered and before dispatch, transforming the event list through hydration, scaling, reordering, linking, fabrication, or filtering.

Normalizers are not modules. They do not extend `Module`, do not subscribe to events, and are never dispatched to; they rewrite the event list in one pass and return it.

## Procedure

### 1. Create The Normalizer Class

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Normalizers/{Name}Normalizer.cs`.

Every normalizer today is declared on the base `CombatLogParser` and lives in `src/FellowshipAnalyzer.Core/Analysis/Normalizers/`; a hero-local normalizer under `Normalizers/` is the exception, so first check whether the transformation belongs in Core.

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
[HeroAnalyzer(HeroName.{Hero})]
[AddNormalizer<{Name}Normalizer>]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

The source generator emits `GetNormalizerTypes()` and DI registration for hero-specific normalizers.

Current execution follows the generated normalizer type list, which preserves `[AddNormalizer<T>]` declaration order with base parser normalizers before hero parser normalizers. Keep `Priority` consistent with the intended order because normalizer implementations use it as documentation. Live Core priorities to place a new normalizer against: `PullBookendNormalizer` -1000, `AbilityMasterDataNormalizer` -100, `ResourceNormalizer` -50, `CastLinkNormalizer` 0.

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

For a rule-driven linker, derive from `EventLinkNormalizer` (`Core/Analysis/Normalizers/`, priority 100)
and supply `EventLink` records; it owns the timestamp scan, the source and target matching, and the
guarantee that each referenced event is linked once per rule. `GundeEventLinkNormalizer` is the example.
Read the links back with `event.RelatedEvents<TEvent>(relation)`.

Link by hand only where a rule cannot express the pairing:

```csharp
public List<Event> Normalize(List<Event> events, int playerId)
{
    var castsByTimestamp = events
        .OfType<CastEvent>()
        .ToLookup(castEvent => (castEvent.Timestamp, castEvent.SourceId, castEvent.Ability.FSLID));

    foreach (var damageEvent in events.OfType<DamageEvent>())
    {
        var matchingCast = castsByTimestamp[(damageEvent.Timestamp, damageEvent.SourceId, damageEvent.Ability.FSLID)]
            .FirstOrDefault();
        if (matchingCast is not null)
        {
            damageEvent.AddRelatedEvent("matching-cast", matchingCast);
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
- Normalizers are constructed by a generator-emitted factory, not resolved from the container, but constructor parameters are still supplied: a sibling module resolves through the parser's module cache, `ParseContext` and `IReadOnlyList<Event>` come from the parser, and any other type falls back to the service provider (as `AbilityMasterDataNormalizer(ReportMasterDataService masterData)` does).

## Checklist

- [ ] File is at `Normalizers/{Name}Normalizer.cs`.
- [ ] Implements `IEventNormalizer` with the current `List<Event>` signature.
- [ ] `[AddNormalizer<T>]` is on the hero parser.
- [ ] Declaration order and `Priority` agree with the intended order.
- [ ] Returns a complete event list.