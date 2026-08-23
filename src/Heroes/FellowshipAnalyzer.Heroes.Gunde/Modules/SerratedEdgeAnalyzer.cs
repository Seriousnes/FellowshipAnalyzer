using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Gunde.Normalizers;

using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class SerratedEdgeAnalyzer : Analyzer
{
    public const int ConsumerGraceMs = 250;

    public const double AdditionalRendConversion = 0.20;

    public static readonly FSLID[] Consumers =
    [
        Spells.HeartSplitter.FSLID,
        Spells.GrimCarve.FSLID,
        Spells.Rupture.FSLID,
        Spells.BloodArc.FSLID,
        Spells.ReaverEdge.FSLID,
        Spells.DoubleStrike.FSLID,
    ];

    private readonly List<SerratedEdgeGrant> _grants = [];

    private int? _grantedAt;
    private CastEvent? _lastCast;
    private List<ConsumerReadiness> _lastCastReadiness = [];

    public GundePullShape Shape => Pull.Targets == PullKind.Single ? GundePullShape.Boss : GundePullShape.Aoe;

    public bool BleedingHeartRingEquipped => Owner.SelectedCombatant.HasItem(Items.BandOfTheBleedingHeart.Id);

    public bool SinisterApronEquipped => Owner.SelectedCombatant.HasItem(Items.CarversSinisterApron.Id);

    public List<int> ConsumerPriority => field ??= BuildConsumerPriority();

    public int PriorityAbilityId => ConsumerPriority[0];

    public List<SerratedEdgeGrant> Grants => _grants;

    public int JudgedGrants => _grants.Count;

    public int PriorityConsumed => Count(SerratedEdgeOutcome.Priority);

    public int AlternateConsumed => Count(SerratedEdgeOutcome.Alternate);

    public int AvoidableFiller => Count(SerratedEdgeOutcome.AvoidableFiller);

    public int ForcedFiller => Count(SerratedEdgeOutcome.ForcedFiller);

    public int Unspent => Count(SerratedEdgeOutcome.Unspent);

    public long TotalRendConverted => _grants.Sum(grant => grant.RendConverted);

    public long RendConvertedBy(SerratedEdgeOutcome outcome) =>
        _grants.Where(grant => grant.Outcome == outcome).Sum(grant => grant.RendConverted);

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.HeartSplitter),
        nameof(Spells.GrimCarve),
        nameof(Spells.Rupture),
        nameof(Spells.BloodArc),
        nameof(Spells.ReaverEdge),
        nameof(Spells.DoubleStrike)])]
    private void OnCandidateCast(CastEvent castEvent)
    {
        _lastCast = castEvent;
        _lastCastReadiness = Snapshot(castEvent.Timestamp);
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnGranted(ApplyBuffEvent buffEvent) => _grantedAt = buffEvent.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnRegranted(RefreshBuffEvent buffEvent) => _grantedAt = buffEvent.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnRemoved(RemoveBuffEvent buffEvent)
    {
        if (_grantedAt is not { } granted) return;

        _grantedAt = null;
        var consumer = ConsumerAt(granted, buffEvent.Timestamp);
        var ability = consumer?.Ability.Id;
        var readiness = consumer is null ? Snapshot(buffEvent.Timestamp) : _lastCastReadiness;
        var rank = ability is { } consumed ? RankOf(consumed) : null;
        var damage = consumer is null ? 0L : SumLinked(consumer, GundeEventLinkNormalizer.CastDamage);

        _grants.Add(new SerratedEdgeGrant(
            granted,
            ability,
            rank,
            readiness,
            Classify(ability, rank, readiness),
            damage,
            (long)Math.Round(damage * AdditionalRendConversion),
            consumer is null ? 0L : SumLinked(consumer, GundeEventLinkNormalizer.Exsanguinate)));
    }

    private CastEvent? ConsumerAt(int granted, int removed) =>
        _lastCast is { } cast && cast.Timestamp >= granted && removed - cast.Timestamp <= ConsumerGraceMs
            ? cast
            : null;

    private static long SumLinked(CastEvent cast, string relation) =>
        cast.RelatedEvents<DamageEvent>(relation).Sum(damageEvent => damageEvent.Amount);

    private List<int> BuildConsumerPriority()
    {
        if (BleedingHeartRingEquipped)
        {
            return [Spells.Rupture.FSLID.Value, Spells.HeartSplitter.FSLID.Value, Spells.GrimCarve.FSLID.Value];
        }

        if (SinisterApronEquipped)
        {
            return Shape == GundePullShape.Aoe
                ? [Spells.GrimCarve.FSLID.Value, Spells.Rupture.FSLID.Value, Spells.HeartSplitter.FSLID.Value]
                : [Spells.Rupture.FSLID.Value, Spells.GrimCarve.FSLID.Value, Spells.HeartSplitter.FSLID.Value];
        }

        return Shape == GundePullShape.Aoe
            ? [Spells.GrimCarve.FSLID.Value, Spells.HeartSplitter.FSLID.Value]
            : [Spells.HeartSplitter.FSLID.Value, Spells.GrimCarve.FSLID.Value];
    }

    private int? RankOf(int ability)
    {
        for (var rank = 0; rank < ConsumerPriority.Count; rank++)
        {
            if (ConsumerPriority[rank] == ability) return rank;
        }

        return null;
    }

    private static SerratedEdgeOutcome Classify(int? consumer, int? rank, List<ConsumerReadiness> readiness) =>
        consumer is null ? SerratedEdgeOutcome.Unspent
        : rank is 0 ? SerratedEdgeOutcome.Priority
        : rank is not null ? SerratedEdgeOutcome.Alternate
        : readiness.Any(entry => entry.Ready) ? SerratedEdgeOutcome.AvoidableFiller
        : SerratedEdgeOutcome.ForcedFiller;

    private List<ConsumerReadiness> Snapshot(int timestamp) =>
    [
        .. ConsumerPriority.Select(ability => new ConsumerReadiness(
            ability,
            SpellUsable.IsAvailable(ability),
            SpellUsable.CooldownRemaining(ability, timestamp)))
    ];

    private int Count(SerratedEdgeOutcome outcome) => _grants.Count(grant => grant.Outcome == outcome);
}

public enum SerratedEdgeOutcome
{
    Priority,

    Alternate,

    AvoidableFiller,

    ForcedFiller,

    Unspent,
}

public readonly record struct ConsumerReadiness(int AbilityId, bool Ready, int RemainingMs);

public sealed record SerratedEdgeGrant(
    int Timestamp,
    int? ConsumerAbilityId,
    int? ConsumerRank,
    List<ConsumerReadiness> Readiness,
    SerratedEdgeOutcome Outcome,
    long ConsumerDamage,
    long RendConverted,
    long ExsanguinateDamage);
