using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Fabricates a <see cref="ResourceChangeEvent"/> for every change in a unit's resources, read from the
/// <see cref="Event.SourceResources"/> and <see cref="Event.TargetResources"/> blocks on events, so a
/// module follows a resource by subscribing to its changes rather than inspecting every event.
/// <para>
/// Each unit's resources are compared with the last block that named the same unit. A resource whose
/// amount or maximum differs, or that the unit's first block reports, becomes one change event, placed
/// immediately before the event whose block revealed it and naming that event as its
/// <see cref="Event.Trigger"/>. Hit points are not a resource and raise no change.
/// </para>
/// <para>
/// Runs at <see cref="Priority"/> 50, after <see cref="ResourceNormalizer"/> (-50) has rescaled the
/// blocks and <see cref="CastLinkNormalizer"/> (0) has dropped duplicate casts, and before
/// <see cref="EventLinkNormalizer"/> (100), so a link rule can name a change event.
/// </para>
/// </summary>
public sealed class ResourceChangeNormalizer : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => 50;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var lastSeen = new Dictionary<(int UnitId, int Instance, ResourceTypes Type), ClassResource>();
        var normalized = new List<Event>(events.Count);

        foreach (var e in events)
        {
            if (e is IHasSourceEvent source)
                AddChanges(normalized, lastSeen, e, source.SourceId, SourceInstance(e), e.SourceResources);

            if (e is IHasTargetEvent target)
                AddChanges(normalized, lastSeen, e, target.TargetId, TargetInstance(e), e.TargetResources);

            normalized.Add(e);
        }

        return normalized;
    }

    private static void AddChanges(
        List<Event> normalized,
        Dictionary<(int UnitId, int Instance, ResourceTypes Type), ClassResource> lastSeen,
        Event revealing,
        int unitId,
        int instance,
        ActorResources? resources)
    {
        if (resources is null) return;

        foreach (var resource in resources.Resources)
        {
            var key = (unitId, instance, resource.Type);
            var hasPrevious = lastSeen.TryGetValue(key, out var previous);
            if (hasPrevious && previous!.Amount == resource.Amount && previous.Max == resource.Max) continue;

            lastSeen[key] = resource;
            normalized.Add(CreateChange(revealing, unitId, resource, hasPrevious ? previous!.Amount : null, resources));
        }
    }

    private static ResourceChangeEvent CreateChange(
        Event revealing,
        int unitId,
        ClassResource resource,
        int? previousAmount,
        ActorResources resources)
    {
        var ability = revealing is not BaseCastEvent && revealing is IAbilityEvent { Ability: { } known }
            ? known
            : Ability.UnknownAbility;

        return new ResourceChangeEvent
        {
            Timestamp = revealing.Timestamp,
            DungeonId = revealing.DungeonId,
            Prepull = revealing.Prepull,
            Fabricated = true,
            Trigger = revealing,
            Ability = ability,
            AbilityGameId = ability.FSLID,
            SourceId = unitId,
            TargetId = revealing is IHasTargetEvent target ? target.TargetId : unitId,
            ResourceChangeType = resource.Type,
            ResourceChange = resource.Amount - (previousAmount ?? 0),
            ResourceAmount = resource.Amount,
            PreviousResourceAmount = previousAmount,
            ResourceMax = resource.Max,
            UnitResources = resources,
        };
    }

    private static int SourceInstance(Event e) =>
        e is IHasSourceWithInstanceEvent { SourceInstance: { } instance } ? instance : 0;

    private static int TargetInstance(Event e) =>
        e is IHasTargetWithInstanceEvent { TargetInstance: { } instance } ? instance : 0;
}
