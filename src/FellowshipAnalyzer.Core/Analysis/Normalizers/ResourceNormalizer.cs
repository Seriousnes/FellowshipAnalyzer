using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Corrects resource values that are scaled by a factor of 100 in Fellowship log data.
/// For example, Winter Orbs appear as 0–500 in logs but are 0–5 in-game.
/// Hit points (<see cref="ActorResources.HitPoints"/> / <see cref="ActorResources.MaxHitPoints"/>)
/// are not scaled and are left unchanged.
/// A <see cref="ClassResource.Max"/> of <c>-100</c> (no-maximum sentinel) becomes <c>-1</c> after normalization.
/// Runs at <see cref="Priority"/> -50, after <see cref="AbilityMasterDataNormalizer"/> (-100)
/// and before <see cref="CastLinkNormalizer"/> (0).
/// Normalizes both <see cref="Event.SourceResources"/> and <see cref="Event.TargetResources"/>.
/// </summary>
public sealed class ResourceNormalizer : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => -50;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        foreach (var e in events)
        {
            NormalizeActorResources(e.SourceResources);
            NormalizeActorResources(e.TargetResources);
        }

        return events;
    }

    private static void NormalizeActorResources(ActorResources? actorResources)
    {
        if (actorResources is null) return;

        foreach (var resource in actorResources.Resources)
        {
            resource.Amount /= 100;
            resource.Max /= 100;
            if (resource.Cost.HasValue)
                resource.Cost = resource.Cost.Value / 100;
        }
    }
}
