using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class SerratedEdgeAnalyzer : Analyzer
{
    public const int ConsumerGraceMs = 250;

    private readonly List<SerratedEdgeGrant> _grants = [];

    private int? _grantedAt;
    private int _lastCastTimestamp = int.MinValue;
    private int _lastCastAbilityId;
    private IReadOnlyList<ConsumerReadiness> _lastCastReadiness = [];

    public GundePullShape Shape => Pull.Targets == PullKind.Single ? GundePullShape.Boss : GundePullShape.Aoe;

    public bool BleedingHeartRingEquipped => Owner.SelectedCombatant.HasItem(Items.BandOfTheBleedingHeart.Id);

    public bool SinisterApronEquipped => Owner.SelectedCombatant.HasItem(Items.CarversSinisterApron.Id);

    public IReadOnlyList<int> ConsumerPriority => field ??= BuildConsumerPriority();

    public int PriorityAbilityId => ConsumerPriority[0];

    public IReadOnlyList<SerratedEdgeGrant> Grants => _grants;

    public int JudgedGrants => _grants.Count;

    public int PriorityConsumed => Count(SerratedEdgeOutcome.Priority);

    public int AlternateConsumed => Count(SerratedEdgeOutcome.Alternate);

    public int AvoidableFiller => Count(SerratedEdgeOutcome.AvoidableFiller);

    public int ForcedFiller => Count(SerratedEdgeOutcome.ForcedFiller);

    public int Unspent => Count(SerratedEdgeOutcome.Unspent);

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.HeartSplitter),
        nameof(Spells.GrimCarve),
        nameof(Spells.Rupture),
        nameof(Spells.BloodArc),
        nameof(Spells.ReaverEdge),
        nameof(Spells.DoubleStrike)])]
    private void OnCandidateCast(CastEvent castEvent)
    {
        _lastCastTimestamp = castEvent.Timestamp;
        _lastCastAbilityId = castEvent.Ability.Id;
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
        var readiness = consumer is null ? Snapshot(buffEvent.Timestamp) : _lastCastReadiness;
        var rank = consumer is { } ability ? RankOf(ability) : null;

        _grants.Add(new SerratedEdgeGrant(
            granted,
            consumer,
            rank,
            readiness,
            Classify(consumer, rank, readiness)));
    }

    private int? ConsumerAt(int granted, int removed) =>
        _lastCastTimestamp >= granted && removed - _lastCastTimestamp <= ConsumerGraceMs
            ? _lastCastAbilityId
            : null;

    private IReadOnlyList<int> BuildConsumerPriority()
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

    private static SerratedEdgeOutcome Classify(int? consumer, int? rank, IReadOnlyList<ConsumerReadiness> readiness) =>
        consumer is null ? SerratedEdgeOutcome.Unspent
        : rank is 0 ? SerratedEdgeOutcome.Priority
        : rank is not null ? SerratedEdgeOutcome.Alternate
        : readiness.Any(entry => entry.Ready) ? SerratedEdgeOutcome.AvoidableFiller
        : SerratedEdgeOutcome.ForcedFiller;

    private IReadOnlyList<ConsumerReadiness> Snapshot(int timestamp) =>
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
    IReadOnlyList<ConsumerReadiness> Readiness,
    SerratedEdgeOutcome Outcome);
