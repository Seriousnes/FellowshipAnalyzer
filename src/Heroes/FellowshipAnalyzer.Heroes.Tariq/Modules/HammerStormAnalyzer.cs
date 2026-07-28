using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class HammerStormAnalyzer : Analyzer
{
    public const int SpinGapMs = 150;

    /// <summary>Damage bursts a full channel lands. The hero data says <c>AoeAttack.MaxAttacks: 4.0</c>; report <c>a:NcqHDKzamL7n6YFv</c> shows three, stable from 100ms to 250ms clustering, in 127 of 129 channels.</summary>
    public const int ExpectedSpins = 3;

    /// <summary>
    /// The target count from which Hammer Storm out-earns Skull Crusher for the Fury it costs. Absent from
    /// the hero data, which carries only <c>AoeAttack.TargetThresholdForDamageScale: 12.0</c> - a damage
    /// scaling point, not a break-even. Measured on report <c>a:NcqHDKzamL7n6YFv</c> by bucketing both
    /// abilities' casts by distinct targets hit and comparing unmitigated damage per point of Fury: Hammer
    /// Storm returns 0.61x a Skull Crusher on one target, 1.18x on two, and rises monotonically from there.
    /// </summary>
    public const int TargetBreakEven = 2;

    public const int MaxChannelDurationMs = 2500;

    private static readonly int HammerStormId = Spells.HammerStorm.FSLID;
    private static readonly int SkullCrusherId = Spells.SkullCrusher.FSLID;

    private readonly ProcLedger _hammerStormProcs = new();
    private readonly ProcLedger _skullCrusherProcs = new();

    private readonly List<TrackedCast> _casts = [];
    private readonly List<SpinTick> _ticks = [];

    private List<HammerStormCast> Evaluated => field ??= Build();

    public IReadOnlyList<HammerStormCast> Casts => Evaluated;

    public int CastCount => Evaluated.Count;

    public int SchismEmpoweredCasts => Evaluated.Count(cast => cast.SchismEmpowered);

    public int TruncatedChannels => Evaluated.Count(cast => cast.Truncated);

    public int CompleteChannels => Evaluated.Count(cast => cast.SpinsCompleted >= ExpectedSpins);

    public int WhiffedCasts => Evaluated.Count(cast => cast.TargetsHit == 0);

    public double AverageTargetsHit
    {
        get
        {
            var connected = Evaluated.Where(cast => cast.TargetsHit > 0).ToList();
            return connected.Count == 0 ? 0d : connected.Average(cast => cast.TargetsHit);
        }
    }

    /// <summary>Channels that caught fewer than <see cref="TargetBreakEven"/> targets, where the Fury would have earned more as Skull Crushers.</summary>
    public int UnderBreakEvenChannels => Evaluated.Count(cast => cast.UnderTargetBreakEven);

    /// <summary>Channels grouped by the number of distinct units their first spin caught, ascending. Target counts hold steady across a channel's spins, so the first spin reads the whole.</summary>
    public IReadOnlyList<TargetCountBucket> TargetsHitDistribution => field ??=
    [
        .. Evaluated
            .Where(cast => cast.TargetsHit > 0)
            .GroupBy(cast => cast.TargetsHit)
            .OrderBy(group => group.Key)
            .Select(group => new TargetCountBucket(group.Key, group.Count())),
    ];

    public SchismProcEconomy SkullCrusherProcs => _skullCrusherProcs.Snapshot();

    public SchismProcEconomy HammerStormProcs => _hammerStormProcs.Snapshot();

    public bool SchismTalented => Owner.SelectedCombatant.HasTalent(TariqTalents.Schism);

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        var abilityId = @event.Ability.Id;
        var empowered = false;

        if (abilityId == HammerStormId)
            empowered = _hammerStormProcs.Consume(@event.Timestamp);
        else if (abilityId == SkullCrusherId)
            _skullCrusherProcs.Consume(@event.Timestamp);

        _casts.Add(new TrackedCast(@event.Timestamp, abilityId, empowered));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spells = new[]
    {
        nameof(Spells.SchismHammerStorm),
        nameof(Spells.SchismSkullCrusher),
    })]
    private void OnProcGained(ApplyBuffEvent @event) => LedgerFor(@event.Ability.Id).Gain();

    [On<RefreshBuffEvent>(To = Actor.Player, Spells = new[]
    {
        nameof(Spells.SchismHammerStorm),
        nameof(Spells.SchismSkullCrusher),
    })]
    private void OnProcOverwritten(RefreshBuffEvent @event) => LedgerFor(@event.Ability.Id).Overwrite();

    [On<RemoveBuffEvent>(To = Actor.Player, Spells = new[]
    {
        nameof(Spells.SchismHammerStorm),
        nameof(Spells.SchismSkullCrusher),
    })]
    private void OnProcRemoved(RemoveBuffEvent @event) => LedgerFor(@event.Ability.Id).Remove(@event.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.HammerStorm),
        nameof(Spells.HammerStormDamage),
        nameof(Spells.HammerStormLightningDamage),
    })]
    private void OnHammerStormDamage(DamageEvent @event) =>
        _ticks.Add(new SpinTick(@event.Timestamp, @event.TargetId, @event.TargetInstance));

    private ProcLedger LedgerFor(int abilityId) =>
        abilityId == Spells.SchismHammerStorm.FSLID ? _hammerStormProcs : _skullCrusherProcs;

    private List<HammerStormCast> Build()
    {
        var built = new List<HammerStormCast>();
        var cursor = 0;

        for (var index = 0; index < _casts.Count; index++)
        {
            var cast = _casts[index];
            if (cast.AbilityId != HammerStormId)
                continue;

            var attributionEnd = AttributionEnd(cast.Timestamp, index);

            while (cursor < _ticks.Count && _ticks[cursor].Timestamp < cast.Timestamp)
                cursor++;

            var spins = ClusterSpins(cursor, attributionEnd);
            var targets = spins.Count == 0 ? 0 : DistinctTargets(spins[0]);
            var truncated = spins.Count < ExpectedSpins && targets > 0;

            built.Add(new HammerStormCast
            {
                Timestamp = cast.Timestamp,
                TargetsHit = targets,
                SpinsCompleted = spins.Count,
                SchismEmpowered = cast.SchismEmpowered,
                NextAbilityId = truncated
                    ? NextAbilityAfter(index, spins[^1][^1].Timestamp, attributionEnd)
                    : null,
            });
        }

        return built;
    }

    private int AttributionEnd(int castTimestamp, int index)
    {
        var end = castTimestamp + MaxChannelDurationMs;

        for (var next = index + 1; next < _casts.Count; next++)
        {
            if (_casts[next].AbilityId != HammerStormId)
                continue;

            return Math.Min(end, _casts[next].Timestamp - 1);
        }

        return end;
    }

    private List<List<SpinTick>> ClusterSpins(int from, int attributionEnd)
    {
        var spins = new List<List<SpinTick>>();

        for (var scan = from; scan < _ticks.Count && _ticks[scan].Timestamp <= attributionEnd; scan++)
        {
            var tick = _ticks[scan];
            if (spins.Count == 0 || tick.Timestamp - spins[^1][^1].Timestamp > SpinGapMs)
                spins.Add([]);

            spins[^1].Add(tick);
        }

        return spins;
    }

    private int? NextAbilityAfter(int index, int lastSpinTimestamp, int attributionEnd)
    {
        var anchorTimestamp = _casts[index].Timestamp;

        for (var next = index + 1; next < _casts.Count; next++)
        {
            var candidate = _casts[next];
            if (candidate.Timestamp > attributionEnd)
                return null;

            if (candidate.Timestamp > anchorTimestamp && candidate.Timestamp >= lastSpinTimestamp)
                return candidate.AbilityId;
        }

        return null;
    }

    private static int DistinctTargets(List<SpinTick> spin)
    {
        var seen = new HashSet<(int TargetId, int? TargetInstance)>();
        foreach (var tick in spin)
            seen.Add((tick.TargetId, tick.TargetInstance));

        return seen.Count;
    }

    private readonly record struct TrackedCast(int Timestamp, int AbilityId, bool SchismEmpowered);

    private readonly record struct SpinTick(int Timestamp, int TargetId, int? TargetInstance);

    private sealed class ProcLedger
    {
        private int _gained;
        private int _consumed;
        private int _overwritten;
        private int _expired;
        private bool _held;
        private bool _awaitingConsumedRemoval;
        private int? _unclaimedRemoval;

        public void Gain()
        {
            _gained++;
            _held = true;
            _awaitingConsumedRemoval = false;
            _unclaimedRemoval = null;
        }

        public void Overwrite()
        {
            _overwritten++;
            _held = true;
        }

        public void Remove(int timestamp)
        {
            if (_awaitingConsumedRemoval)
            {
                _awaitingConsumedRemoval = false;
            }
            else if (_held)
            {
                _expired++;
                _unclaimedRemoval = timestamp;
            }

            _held = false;
        }

        public bool Consume(int timestamp)
        {
            if (_held)
            {
                _consumed++;
                _held = false;
                _awaitingConsumedRemoval = true;
                _unclaimedRemoval = null;
                return true;
            }

            if (_unclaimedRemoval != timestamp)
                return false;

            _expired--;
            _consumed++;
            _unclaimedRemoval = null;
            return true;
        }

        public SchismProcEconomy Snapshot() => new(_gained, _consumed, _overwritten, _expired);
    }
}

public sealed record HammerStormCast
{
    public required int Timestamp { get; init; }

    public required int TargetsHit { get; init; }

    public required int SpinsCompleted { get; init; }

    public required bool SchismEmpowered { get; init; }

    /// <summary>The first ability cast at or after this channel's last spin. Named as a fact about cast order; the hero data says nothing about which abilities cancel a channel.</summary>
    public int? NextAbilityId { get; init; }

    public bool Truncated => SpinsCompleted < HammerStormAnalyzer.ExpectedSpins && TargetsHit > 0;

    /// <summary>The channel connected with fewer than <see cref="HammerStormAnalyzer.TargetBreakEven"/> targets, so its Fury would have earned more as Skull Crushers.</summary>
    public bool UnderTargetBreakEven => TargetsHit > 0 && TargetsHit < HammerStormAnalyzer.TargetBreakEven;
}

public readonly record struct SchismProcEconomy(int Gained, int Consumed, int Overwritten, int Expired);

public readonly record struct TargetCountBucket(int TargetsHit, int Channels);
