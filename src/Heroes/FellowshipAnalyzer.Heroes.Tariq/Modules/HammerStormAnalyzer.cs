using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures Tariq's Hammer Storm channels on every pull, regardless of shape. Hammer Storm spends 50
/// Fury on a three-spin channel whose spins escalate, so it is the wrong spender below
/// <see cref="AoeTargetThreshold"/> targets unless a Schism proc empowers it, and a channel cut short
/// by another button forfeits the spins that were about to hit hardest. Both are measured here, along
/// with the Schism proc economy that decides which spender is correct on a given global.
/// </summary>
/// <remarks>
/// A channel is reconstructed from its <see cref="CastEvent"/> and the damage that follows, never from
/// the channel bookend events: Hammer Storm logs a <c>beginchannel</c> alongside the cast, but the
/// matching <c>endchannel</c> is effectively absent (one occurrence in 129 channels in the Season 3
/// reference log), so nothing may key off it. Damage is attributed to the cast it follows until the
/// next Hammer Storm cast or <see cref="MaxChannelDurationMs"/>, whichever comes first, and is then
/// clustered into spins on <see cref="SpinGapMs"/>.
/// <para>
/// Targets are counted on the first spin alone. Every spin of a channel hits whatever is in range at
/// the time, so a later spin reflects how the pack moved rather than the choice the player made when
/// they pressed the button; the first spin is the one the spender decision was actually taken against.
/// </para>
/// <para>
/// A channel that produced no damage at all is a whiff rather than a truncation - see
/// <see cref="HammerStormCast.Truncated"/>. A truncation is attributed to the first other player cast
/// that landed at or after the last observed spin, because an ability pressed before a spin that then
/// happened plainly did not stop the channel. <see cref="HammerStormCast.TruncatingAbilityId"/> is
/// therefore null exactly when no cast of the player's fell in that window, which a channel the pull
/// ends inside reliably produces. It is only a correlation, never a proof: a channel whose pack died
/// mid-spin (non-selected enemy deaths are not logged at all) still picks up whatever the player cast
/// next, and only Culling Strike and Leap Smash are off the global cooldown far enough to cancel a
/// channel they land inside.
/// </para>
/// <para>
/// Schism is tracked in both directions from the player's own buff stream. A proc is gained on
/// <c>applybuff</c>, overwritten on <c>refreshbuff</c> (a roll that landed on a proc already held, so
/// the held value survived but the new roll added nothing), consumed by a cast of the spender it
/// empowers, and expired on any other <c>removebuff</c>. Consumption and empowerment are decided
/// together from one reading so they can never disagree, and a removal logged at the same millisecond
/// as the consuming cast is reclaimed as a consumption regardless of which of the two the log emitted
/// first.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class HammerStormAnalyzer : Analyzer
{
    /// <summary>
    /// Milliseconds of silence that separate one spin from the next. Consecutive spins landed 270-540ms
    /// apart in the Season 3 reference log (the spacing is haste-scaled), while the damage events of a
    /// single spin - one per target hit - arrive within a few milliseconds of each other, so 150ms sits
    /// clear of both and split 127 of 129 logged channels into exactly three spins.
    /// </summary>
    public const int SpinGapMs = 150;

    /// <summary>
    /// Spins a complete Hammer Storm channel lands. The Season 3 patch notes describe the channel as
    /// 1.5 seconds of three spins with each successive spin 35 percent stronger, and the reference log
    /// confirms three damage clusters on all but the channels that were cut short.
    /// </summary>
    public const int ExpectedSpins = 3;

    /// <summary>
    /// Targets at or above which Hammer Storm out-damages Skull Crusher. Season 3 guide-site consensus
    /// (Icy Veins and Method.gg) over the Kit's spender scaling: below three targets the escalating
    /// three-spin channel loses to a single Skull Crusher unless a Schism proc empowers it.
    /// </summary>
    public const int AoeTargetThreshold = 3;

    /// <summary>
    /// Milliseconds after the cast beyond which damage is no longer this channel's. The channel is
    /// 1.5 seconds at base and shortens with haste, so 2.5 seconds covers the slowest realistic channel
    /// plus the lag between the final spin and its damage events while still ending long before the next
    /// ability's damage could be mistaken for a fourth spin.
    /// </summary>
    public const int MaxChannelDurationMs = 2500;

    private static readonly int HammerStormId = Spells.HammerStorm.FSLID;
    private static readonly int SkullCrusherId = Spells.SkullCrusher.FSLID;

    private readonly ProcLedger _hammerStormProcs = new();
    private readonly ProcLedger _skullCrusherProcs = new();

    private readonly List<TrackedCast> _casts = [];
    private readonly List<SpinTick> _ticks = [];

    private List<HammerStormCast>? _evaluated;
    private List<HammerStormCast> Evaluated => _evaluated ??= Build();

    /// <summary>Every Hammer Storm channel in the pull, in the order it was cast.</summary>
    public IReadOnlyList<HammerStormCast> Casts => Evaluated;

    public int CastCount => Evaluated.Count;

    /// <summary>
    /// Channels that hit fewer than <see cref="AoeTargetThreshold"/> targets without a Schism proc to
    /// justify them. Whiffs are excluded: a channel that connected with nothing was not a spender choice
    /// that can be second-guessed on target count.
    /// </summary>
    public int LowTargetCasts => Evaluated.Count(cast =>
        cast.TargetsHit > 0 && cast.TargetsHit < AoeTargetThreshold && !cast.SchismEmpowered);

    /// <summary>Channels a Schism proc empowered.</summary>
    public int SchismEmpoweredCasts => Evaluated.Count(cast => cast.SchismEmpowered);

    /// <summary>Channels that landed damage but stopped before their third spin.</summary>
    public int TruncatedChannels => Evaluated.Count(cast => cast.Truncated);

    /// <summary>Channels that landed all <see cref="ExpectedSpins"/> spins.</summary>
    public int CompleteChannels => Evaluated.Count(cast => cast.SpinsCompleted >= ExpectedSpins);

    /// <summary>Channels that produced no damage at all.</summary>
    public int WhiffedCasts => Evaluated.Count(cast => cast.TargetsHit == 0);

    /// <summary>
    /// Mean first-spin targets over the channels that connected, or zero when none did.
    /// </summary>
    public double AverageTargetsHit
    {
        get
        {
            var connected = Evaluated.Where(cast => cast.TargetsHit > 0).ToList();
            return connected.Count == 0 ? 0d : connected.Average(cast => cast.TargetsHit);
        }
    }

    /// <summary>
    /// The Schism procs that Hammer Storm casts roll and Skull Crusher casts spend.
    /// </summary>
    public SchismProcEconomy SkullCrusherProcs => _skullCrusherProcs.Snapshot();

    /// <summary>
    /// The Schism procs that Skull Crusher casts roll and Hammer Storm casts spend.
    /// </summary>
    public SchismProcEconomy HammerStormProcs => _hammerStormProcs.Snapshot();

    /// <summary>
    /// Whether the player took Schism. The parser substitutes an empty combatantinfo when the log
    /// carries none for the selected player, so an unknown build reads as untalented.
    /// </summary>
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
                TruncatingAbilityId = truncated
                    ? TruncatingAbility(index, spins[^1][^1].Timestamp, attributionEnd)
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

    private int? TruncatingAbility(int index, int lastSpinTimestamp, int attributionEnd)
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

    /// <summary>
    /// Running state for one direction of the Schism proc economy. <see cref="Consume"/> both answers
    /// whether the spender was empowered and records the consumption, so the per-cast flag and the
    /// counters are always the same reading.
    /// </summary>
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

/// <summary>One Hammer Storm channel, reconstructed from its cast and the damage that followed.</summary>
public sealed record HammerStormCast
{
    /// <summary>Fight-relative timestamp of the cast that opened the channel.</summary>
    public required int Timestamp { get; init; }

    /// <summary>
    /// Distinct units the channel's first spin hit, or zero when the channel produced no damage.
    /// </summary>
    public required int TargetsHit { get; init; }

    /// <summary>Damage bursts observed for this channel, one per spin.</summary>
    public required int SpinsCompleted { get; init; }

    /// <summary>Whether a Schism proc empowered this channel.</summary>
    public required bool SchismEmpowered { get; init; }

    /// <summary>
    /// The first other player ability cast at or after this channel's last spin, when the channel was
    /// truncated. Null when the channel completed, whiffed, or no cast of the player's landed in that
    /// window at all.
    /// </summary>
    public int? TruncatingAbilityId { get; init; }

    /// <summary>
    /// Whether the channel landed damage and then stopped before its third spin. A channel that
    /// produced no damage at all is a whiff rather than a truncation: nothing was interrupted, because
    /// nothing ever connected.
    /// </summary>
    public bool Truncated => SpinsCompleted < HammerStormAnalyzer.ExpectedSpins && TargetsHit > 0;

    /// <summary>Whether the channel hit enough targets to be the correct spender on its own.</summary>
    public bool MetAoeThreshold => TargetsHit >= HammerStormAnalyzer.AoeTargetThreshold;
}

/// <summary>
/// One direction of the Schism proc economy over a pull: the buff empowering a single spender, counted
/// from the player's own buff stream.
/// </summary>
/// <param name="Gained">Fresh procs, rolled while no proc of this direction was held.</param>
/// <param name="Consumed">Procs spent by a cast of the spender they empower.</param>
/// <param name="Overwritten">
/// Rolls that landed on a proc already held. The held proc survived, so nothing was lost, but the roll
/// added nothing either.
/// </param>
/// <param name="Expired">Procs that fell off without the spender they empower being cast.</param>
public readonly record struct SchismProcEconomy(int Gained, int Consumed, int Overwritten, int Expired);
