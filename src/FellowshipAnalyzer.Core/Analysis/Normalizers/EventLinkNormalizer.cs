using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Establishes named associations between events that the log itself leaves unrelated, so an analyzer
/// can read a cast's own damage, or a debuff's applying cast, without re-deriving the pairing from
/// timestamps. A subclass supplies the <see cref="EventLink"/> rules; this class applies them.
/// <para>
/// Each rule is applied in one pass over the stream. For every event matching the rule's linking
/// criteria, the scan runs forward to <see cref="EventLink.ForwardBufferMs"/> and back to
/// <see cref="EventLink.BackwardBufferMs"/>, adding every event that matches the referenced criteria
/// to the linking event under <see cref="EventLink.Relation"/>.
/// </para>
/// <para>
/// A scan also stops at the next event matching the same rule's linking criteria from the same source,
/// so a second cast of one ability does not reach back over the first for its damage. That is what lets
/// a rule use a buffer wide enough for the slowest cast in the stream.
/// </para>
/// <para>
/// Within one rule a referenced event is linked at most once, to the earliest linking event in the
/// stream that matches it. A sum over a relation therefore counts each referenced event once, which the
/// buffers alone cannot guarantee once a rule scans backward.
/// </para>
/// </summary>
/// <remarks>Creates a normalizer applying <paramref name="links"/>, in the order given.</remarks>
/// <param name="links">The rules to apply. A later rule sees the links an earlier one established.</param>
public abstract class EventLinkNormalizer(List<EventLink> links) : IEventNormalizer
{
    private readonly List<EventLink> _links = links;

    /// <inheritdoc/>
    public virtual int Priority => 100;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        foreach (var link in _links)
        {
            Apply(link, events);
        }

        return events;
    }

    private static void Apply(EventLink link, List<Event> events)
    {
        var claimed = new HashSet<Event>(ReferenceEqualityComparer.Instance);

        for (var index = 0; index < events.Count; index++)
        {
            var linking = events[index];
            if (!IsLinking(link, linking)) continue;

            for (var forward = index + 1; forward < events.Count; forward++)
            {
                var candidate = events[forward];
                if (candidate.Timestamp - linking.Timestamp > link.ForwardBufferMs) break;
                if (IsBoundary(link, linking, candidate)) break;
                TryLink(link, linking, candidate, claimed);
            }

            for (var backward = index - 1; backward >= 0; backward--)
            {
                var candidate = events[backward];
                if (linking.Timestamp - candidate.Timestamp > link.BackwardBufferMs) break;
                if (IsBoundary(link, linking, candidate)) break;
                TryLink(link, linking, candidate, claimed);
            }
        }
    }

    private static void TryLink(EventLink link, Event linking, Event candidate, HashSet<Event> claimed)
    {
        if (!link.ReferencedEventType.IsInstanceOfType(candidate)) return;
        if (!MatchesAbility(link.ReferencedAbilityIds, candidate)) return;
        if (!SourceMatches(link, linking, candidate)) return;
        if (!TargetMatches(link, linking, candidate)) return;
        if (!claimed.Add(candidate)) return;

        linking.AddRelatedEvent(link.Relation, candidate);
    }

    private static bool IsBoundary(EventLink link, Event linking, Event candidate) =>
        IsLinking(link, candidate)
        && SourceMatches(link, linking, candidate)
        && TargetMatches(link, linking, candidate);

    private static bool IsLinking(EventLink link, Event candidate) =>
        link.LinkingEventType.IsInstanceOfType(candidate) && MatchesAbility(link.LinkingAbilityIds, candidate);

    private static bool MatchesAbility(List<FSLID>? abilityIds, Event candidate)
    {
        if (abilityIds is null) return true;
        if (candidate is not IAbilityEvent abilityEvent) return false;

        var id = abilityEvent.Ability is { FSLID.Value: not 0 } ability ? ability.FSLID : abilityEvent.AbilityGameId;
        for (var index = 0; index < abilityIds.Count; index++)
        {
            if (abilityIds[index] == id) return true;
        }

        return false;
    }

    private static bool SourceMatches(EventLink link, Event linking, Event candidate) =>
        link.AnySource ||
        (linking is IHasSourceEvent linkingSource
            && candidate is IHasSourceEvent candidateSource
            && linkingSource.SourceId == candidateSource.SourceId);

    private static bool TargetMatches(EventLink link, Event linking, Event candidate) =>
        link.AnyTarget ||
        (linking is IHasTargetEvent linkingTarget
            && candidate is IHasTargetEvent candidateTarget
            && linkingTarget.TargetId == candidateTarget.TargetId
            && TargetInstance(linking) == TargetInstance(candidate));

    private static int TargetInstance(Event candidate) =>
        candidate is IHasTargetWithInstanceEvent { TargetInstance: { } instance } ? instance : 0;
}
