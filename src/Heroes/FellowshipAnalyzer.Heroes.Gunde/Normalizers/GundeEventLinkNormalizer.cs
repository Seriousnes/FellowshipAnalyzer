using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

namespace FellowshipAnalyzer.Heroes.Gunde.Normalizers;

public sealed class GundeEventLinkNormalizer() : EventLinkNormalizer(BuildLinks())
{
    public const string CastDamage = nameof(CastDamage);

    public const string Exsanguinate = nameof(Exsanguinate);

    private const int CastDamageWindowMs = 2_500;

    private static IReadOnlyList<EventLink> BuildLinks() =>
    [
        .. SerratedEdgeAnalyzer.Consumers.Select(ability => new EventLink
        {
            Relation = CastDamage,
            LinkingEventType = typeof(CastEvent),
            LinkingAbilityIds = [ability],
            ReferencedEventType = typeof(DamageEvent),
            ReferencedAbilityIds = [ability],
            ForwardBufferMs = CastDamageWindowMs,
            AnyTarget = true,
        }),
        new EventLink
        {
            Relation = Exsanguinate,
            LinkingEventType = typeof(CastEvent),
            LinkingAbilityIds = [Spells.HeartSplitter.FSLID],
            ReferencedEventType = typeof(DamageEvent),
            ReferencedAbilityIds = [Spells.HeartSplitterDotBonusDamage.FSLID],
            ForwardBufferMs = CastDamageWindowMs,
            AnyTarget = true,
        },
    ];
}
