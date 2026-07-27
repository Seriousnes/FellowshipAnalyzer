using FellowshipAnalyzer.Core.Analysis;
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
    private CooldownSnapshot _lastCastCooldowns;

    public GundePullShape Shape => Pull.Targets == PullKind.Single ? GundePullShape.Boss : GundePullShape.Aoe;

    public int PriorityAbilityId =>
        Shape == GundePullShape.Aoe ? Spells.GrimCarve.FSLID.Value : Spells.HeartSplitter.FSLID.Value;

    public int AlternateAbilityId =>
        Shape == GundePullShape.Aoe ? Spells.HeartSplitter.FSLID.Value : Spells.GrimCarve.FSLID.Value;

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
        _lastCastCooldowns = Snapshot(castEvent.Timestamp);
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
        var cooldowns = consumer is null ? Snapshot(buffEvent.Timestamp) : _lastCastCooldowns;

        _grants.Add(new SerratedEdgeGrant(
            granted,
            consumer,
            cooldowns.HeartSplitterReady,
            cooldowns.HeartSplitterRemainingMs,
            cooldowns.GrimCarveReady,
            cooldowns.GrimCarveRemainingMs,
            Classify(consumer, cooldowns)));
    }

    private int? ConsumerAt(int granted, int removed) =>
        _lastCastTimestamp >= granted && removed - _lastCastTimestamp <= ConsumerGraceMs
            ? _lastCastAbilityId
            : null;

    private SerratedEdgeOutcome Classify(int? consumer, CooldownSnapshot cooldowns)
    {
        if (consumer is not { } ability) return SerratedEdgeOutcome.Unspent;
        if (ability == PriorityAbilityId) return SerratedEdgeOutcome.Priority;
        if (ability == AlternateAbilityId) return SerratedEdgeOutcome.Alternate;

        return cooldowns.HeartSplitterReady || cooldowns.GrimCarveReady
            ? SerratedEdgeOutcome.AvoidableFiller
            : SerratedEdgeOutcome.ForcedFiller;
    }

    private CooldownSnapshot Snapshot(int timestamp) => new(
        SpellUsable.IsAvailable(Spells.HeartSplitter.FSLID.Value),
        SpellUsable.CooldownRemaining(Spells.HeartSplitter.FSLID.Value, timestamp),
        SpellUsable.IsAvailable(Spells.GrimCarve.FSLID.Value),
        SpellUsable.CooldownRemaining(Spells.GrimCarve.FSLID.Value, timestamp));

    private int Count(SerratedEdgeOutcome outcome) => _grants.Count(grant => grant.Outcome == outcome);

    private readonly record struct CooldownSnapshot(
        bool HeartSplitterReady,
        int HeartSplitterRemainingMs,
        bool GrimCarveReady,
        int GrimCarveRemainingMs);
}

public enum SerratedEdgeOutcome
{
    Priority,

    Alternate,

    AvoidableFiller,

    ForcedFiller,

    Unspent,
}

public sealed record SerratedEdgeGrant(
    int Timestamp,
    int? ConsumerAbilityId,
    bool HeartSplitterReady,
    int HeartSplitterRemainingMs,
    bool GrimCarveReady,
    int GrimCarveRemainingMs,
    SerratedEdgeOutcome Outcome);
