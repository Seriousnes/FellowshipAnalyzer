using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures how Elarion's cooldowns were stacked against each other during a pull.
/// <para>
/// <see cref="Spells.EventHorizon"/> is gated on Spirit rather than a cooldown, while
/// <see cref="Spells.SkystridersGrace"/> is a flat 120 second recharge, so the two can be lined up
/// every time the ultimate comes round: the haste buff and the damage buff overlap into one double
/// window. Each Event Horizon cast is paired with the nearest Skystrider's Grace cast of the pull, so
/// two ultimates cast close together both point at the Grace between them rather than one of them
/// being scored as unpaired.
/// </para>
/// <para>
/// <see cref="Spells.LunarlightMark"/> and <see cref="Spells.StarfallVolley"/> both recharge in 40
/// seconds, so they come round together and the volley can be dropped onto freshly marked enemies.
/// Neither cooldown is simulated: the volley's recharge is compressed by Event Horizon and by the
/// Repeating Stars talent, so a simulated cooldown would drift away from the log. Both are measured
/// from cast events only, with <see cref="AchievableFortySecondCasts"/> as the ceiling a 40 second
/// recharge allows on a pull of this length.
/// </para>
/// <para>
/// Grace is the one cooldown worth simulating, since nothing in the kit accelerates it. Time it spent
/// available and unused is read from the <see cref="UpdateSpellUsableEvent"/> stream
/// <see cref="SpellUsable"/> fabricates, taking the true recharge instant from
/// <see cref="UpdateSpellUsableEvent.ExpectedRechargeTimestamp"/> rather than the observing timestamp.
/// A hold still running at pull end, or a buff with no logged removal, closes at
/// <see cref="Pull.EndTime"/>.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class CooldownPairingAnalyzer : Analyzer
{
    /// <summary>The shared Lunarlight Mark and Starfall Volley recharge, in milliseconds.</summary>
    public const int SharedRechargeMs = 40_000;

    private readonly List<int> _graceCasts = [];
    private readonly List<int> _eventHorizonCasts = [];
    private readonly List<int> _markCasts = [];
    private readonly List<int> _volleyCasts = [];

    private readonly BuffUptime _graceBuff = new();
    private readonly BuffUptime _eventHorizonBuff = new();

    private int _graceHeldMs;
    private int? _graceAvailableSince;

    private List<EventHorizonPairing>? _eventHorizonPairings;
    private List<MarkVolleyPairing>? _markVolleyPairings;

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    /// <summary>Skystrider's Grace casts made during the pull.</summary>
    public int GraceCasts => _graceCasts.Count;

    /// <summary>Event Horizon casts made during the pull.</summary>
    public int EventHorizonCasts => _eventHorizonCasts.Count;

    /// <summary>Lunarlight Mark casts made during the pull.</summary>
    public int MarkCasts => _markCasts.Count;

    /// <summary>Starfall Volley casts made during the pull.</summary>
    public int VolleyCasts => _volleyCasts.Count;

    /// <summary>
    /// Milliseconds Skystrider's Grace spent off cooldown without being cast. Every one of them is haste
    /// uptime the pull never received.
    /// </summary>
    public int GraceHeldMs =>
        _graceHeldMs + (_graceAvailableSince is { } since ? Math.Max(0, Pull.EndTime - since) : 0);

    /// <summary>Milliseconds the Skystrider's Grace haste buff was up during the pull.</summary>
    public int GraceBuffUptimeMs => _graceBuff.TotalAt(Pull.EndTime);

    /// <summary>Milliseconds the Event Horizon buff was up during the pull.</summary>
    public int EventHorizonBuffUptimeMs => _eventHorizonBuff.TotalAt(Pull.EndTime);

    /// <summary>One entry per Event Horizon cast, in cast order, carrying its distance to the nearest Grace cast.</summary>
    public IReadOnlyList<EventHorizonPairing> EventHorizonPairings =>
        _eventHorizonPairings ??=
        [
            .. _eventHorizonCasts.Select(cast => new EventHorizonPairing(cast, NearestDeltaMs(cast, _graceCasts))),
        ];

    /// <summary>One entry per Lunarlight Mark cast, in cast order, carrying its distance to the nearest Starfall Volley cast.</summary>
    public IReadOnlyList<MarkVolleyPairing> MarkVolleyPairings =>
        _markVolleyPairings ??=
        [
            .. _markCasts.Select(cast => new MarkVolleyPairing(cast, NearestDeltaMs(cast, _volleyCasts))),
        ];

    /// <summary>
    /// Casts a 40 second recharge allows on a pull of this length, counting the one available at the
    /// pull's start. Context for the Lunarlight Mark and Starfall Volley counts rather than a target,
    /// since a pull can end before the last recharge finishes.
    /// </summary>
    public int AchievableFortySecondCasts => PullDurationMs / SharedRechargeMs + 1;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SkystridersGrace))]
    private void OnGraceCast(CastEvent e) => _graceCasts.Add(e.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EventHorizon))]
    private void OnEventHorizonCast(CastEvent e) => _eventHorizonCasts.Add(e.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMark))]
    private void OnMarkCast(CastEvent e) => _markCasts.Add(e.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.StarfallVolley))]
    private void OnVolleyCast(CastEvent e) => _volleyCasts.Add(e.Timestamp);

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.SkystridersGrace))]
    private void OnGraceUsable(UpdateSpellUsableEvent e)
    {
        if (e.UpdateType == UpdateSpellUsableType.EndCooldown)
        {
            _graceAvailableSince ??= e.ExpectedRechargeTimestamp;
        }
        else if (e.UpdateType == UpdateSpellUsableType.BeginCooldown && _graceAvailableSince is { } since)
        {
            _graceHeldMs += Math.Max(0, e.Timestamp - since);
            _graceAvailableSince = null;
        }
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersGraceBuff))]
    private void OnGraceBuffApplied(ApplyBuffEvent e) => _graceBuff.Open(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SkystridersGraceBuff))]
    private void OnGraceBuffRemoved(RemoveBuffEvent e) => _graceBuff.Close(e.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnEventHorizonBuffApplied(ApplyBuffEvent e) => _eventHorizonBuff.Open(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnEventHorizonBuffRemoved(RemoveBuffEvent e) => _eventHorizonBuff.Close(e.Timestamp);

    private static int? NearestDeltaMs(int timestamp, List<int> candidates)
    {
        int? nearest = null;
        foreach (var candidate in candidates)
        {
            var delta = Math.Abs(timestamp - candidate);
            if (nearest is null || delta < nearest)
                nearest = delta;
        }

        return nearest;
    }

    /// <summary>
    /// One Event Horizon cast and its distance to the nearest Skystrider's Grace cast of the pull.
    /// <paramref name="NearestGraceDeltaMs"/> is null when Grace was never cast during the pull.
    /// </summary>
    public sealed record EventHorizonPairing(int Timestamp, int? NearestGraceDeltaMs);

    /// <summary>
    /// One Lunarlight Mark cast and its distance to the nearest Starfall Volley cast of the pull.
    /// <paramref name="NearestVolleyDeltaMs"/> is null when the volley was never cast during the pull.
    /// </summary>
    public sealed record MarkVolleyPairing(int Timestamp, int? NearestVolleyDeltaMs);

    private sealed class BuffUptime
    {
        private int _totalMs;
        private int? _openSince;

        public void Open(int timestamp) => _openSince ??= timestamp;

        public void Close(int timestamp)
        {
            if (_openSince is not { } start)
                return;

            _totalMs += Math.Max(0, timestamp - start);
            _openSince = null;
        }

        public int TotalAt(int pullEnd) =>
            _totalMs + (_openSince is { } start ? Math.Max(0, pullEnd - start) : 0);
    }
}
