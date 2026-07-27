using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class CooldownPairingAnalyzer : Analyzer
{
    public const int SharedRechargeMs = 40_000;

    private readonly List<int> _graceCasts = [];
    private readonly List<int> _eventHorizonCasts = [];
    private readonly List<int> _markCasts = [];
    private readonly List<int> _volleyCasts = [];

    private readonly BuffUptime _graceBuff = new();
    private readonly BuffUptime _eventHorizonBuff = new();

    private int _graceHeldMs;
    private int? _graceAvailableSince;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    public int GraceCasts => _graceCasts.Count;

    public int EventHorizonCasts => _eventHorizonCasts.Count;

    public int MarkCasts => _markCasts.Count;

    public int VolleyCasts => _volleyCasts.Count;

    public int GraceHeldMs =>
        _graceHeldMs + (_graceAvailableSince is { } since ? Math.Max(0, Pull.EndTime - since) : 0);

    public int GraceBuffUptimeMs => _graceBuff.TotalAt(Pull.EndTime);

    public int EventHorizonBuffUptimeMs => _eventHorizonBuff.TotalAt(Pull.EndTime);

    public IReadOnlyList<EventHorizonPairing> EventHorizonPairings =>
        field ??=
        [
            .. _eventHorizonCasts.Select(cast => new EventHorizonPairing(cast, NearestDeltaMs(cast, _graceCasts))),
        ];

    public IReadOnlyList<MarkVolleyPairing> MarkVolleyPairings =>
        field ??=
        [
            .. _markCasts.Select(cast => new MarkVolleyPairing(cast, NearestDeltaMs(cast, _volleyCasts))),
        ];

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

    public sealed record EventHorizonPairing(int Timestamp, int? NearestGraceDeltaMs);

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
