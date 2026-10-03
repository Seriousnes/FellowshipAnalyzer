using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Aeona.Normalizers;

public sealed class AeonaEventLinkNormalizer() : EventLinkNormalizer(BuildLinks())
{
    /// <summary>
    /// Links a Temporal Barrage bolt's heal to the change in its target's Stagger that the same heal
    /// revealed.
    /// </summary>
    public const string BarrageStagger = nameof(BarrageStagger);

    private static List<EventLink> BuildLinks() =>
    [
        new EventLink
        {
            Relation = BarrageStagger,
            LinkingEventType = typeof(HealEvent),
            LinkingAbilityIds = [Spells.TemporalBarrage.FSLID],
            ReferencedEventType = typeof(ResourceChangeEvent),
            ReferencedAbilityIds = [Spells.TemporalBarrage.FSLID],
            AnySource = true,
            AnyTarget = true,
            AdditionalCondition = IsStaggerOfHealTarget,
        },
    ];

    private static bool IsStaggerOfHealTarget(Event linking, Event referenced) =>
        linking is HealEvent heal
        && referenced is ResourceChangeEvent { ResourceChangeType: ResourceTypes.Stagger } change
        && change.SourceId == heal.TargetId
        && ReferenceEquals(change.Trigger, heal);
}
