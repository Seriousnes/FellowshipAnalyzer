using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures what Hold the Line's empowerment turned into. Hold the Line empowers the next Shield
/// Slam, and that Shield Slam lays a barrier; the barrier's own removal carries whatever absorb it
/// still held, so a barrier that expired with absorb left is measurable waste rather than an
/// inference.
/// <para>
/// The empowerment's source is settled from the log, not from the spell data: the buff lands within a
/// millisecond of every Hold the Line cast in the validation report, while the movement buff the
/// Season 3 dump attributes to Hold the Line never appears at all.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EmpoweredShieldSlamAnalyzer : Analyzer
{
    private readonly List<BarrierCapture> _barriers = [];

    private BarrierCapture? _open;
    private int _empowermentsGranted;
    private int _empowermentsExpired;
    private int _barrierApplications;
    private bool _empowermentOpen;
    private bool _empowermentConsumed;

    /// <summary>Hold the Line casts that empowered a Shield Slam this pull.</summary>
    public int EmpowermentsGranted => _empowermentsGranted;

    /// <summary>
    /// Empowerments that fell off with no Shield Slam cast under them. Zero on every pull of the
    /// validation report, because the buff is removed the instant a Shield Slam spends it and Shield
    /// Slam comes round faster than the buff can lapse - so treat a non-zero reading as the signal and
    /// zero as the expected case, never as a score.
    /// </summary>
    public int EmpowermentsExpired => _empowermentsExpired;

    /// <summary>
    /// Barrier applications, counting a refresh onto a live barrier alongside a fresh one. Every
    /// application in the validation report landed inside an empowerment window with a Shield Slam
    /// within half a second, and no application fell outside one, so the barrier has no second source.
    /// </summary>
    public int BarrierApplications => _barrierApplications;

    /// <summary>
    /// Barrier windows this pull. A barrier laid while one is already up refreshes it rather than
    /// stacking, so consecutive empowered Shield Slams inside one duration share a window and
    /// <see cref="BarrierApplications"/> runs ahead of this.
    /// </summary>
    public int BarrierWindows => Result.Barriers.Count;

    /// <summary>Every barrier this pull, in encounter order.</summary>
    public IReadOnlyList<BarrierUse> Barriers => Result.Barriers;

    /// <summary>Damage the barriers absorbed.</summary>
    public long AbsorbUsed => Result.AbsorbUsed;

    /// <summary>Absorb still on a barrier when it expired.</summary>
    public long AbsorbWasted => Result.AbsorbWasted;

    /// <summary>Barriers that expired still holding absorb.</summary>
    public int BarriersExpiredUnspent => Result.ExpiredUnspent;

    /// <summary>Share (0-1) of the barriers' absorb that was consumed rather than left to expire.</summary>
    public double AbsorbEfficiency
    {
        get
        {
            var offered = Result.AbsorbUsed + Result.AbsorbWasted;
            return offered > 0 ? (double)Result.AbsorbUsed / offered : 0;
        }
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorbBuffSelfBuff))]
    private void OnEmpowered(ApplyBuffEvent buffEvent)
    {
        _empowermentsGranted++;
        _empowermentOpen = true;
        _empowermentConsumed = false;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlam))]
    private void OnShieldSlamCast(CastEvent castEvent)
    {
        if (_empowermentOpen) _empowermentConsumed = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorbBuffSelfBuff))]
    private void OnEmpowermentRemoved(RemoveBuffEvent buffEvent)
    {
        if (_empowermentOpen && !_empowermentConsumed) _empowermentsExpired++;

        _empowermentOpen = false;
        _empowermentConsumed = false;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierApplied(ApplyBuffEvent buffEvent) => OpenBarrier(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRefreshed(RefreshBuffEvent buffEvent) => OpenBarrier(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRemoved(RemoveBuffEvent buffEvent)
    {
        if (_open is null) return;

        _open.End = buffEvent.Timestamp;
        _open.Remaining = buffEvent.Absorb ?? 0;
        _open = null;
    }

    [On<AbsorbedEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent)
    {
        if (_open is null) return;
        _open.Absorbed += absorbedEvent.Amount;
    }

    private void OpenBarrier(int timestamp)
    {
        _barrierApplications++;

        if (_open is not null) return;

        _open = new BarrierCapture { Start = timestamp, End = timestamp };
        _barriers.Add(_open);
    }

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        var barriers = new List<BarrierUse>(_barriers.Count);
        long used = 0;
        long wasted = 0;
        var expiredUnspent = 0;

        foreach (var capture in _barriers)
        {
            var truncated = ReferenceEquals(capture, _open);
            var end = truncated ? Math.Max(capture.End, Pull.EndTime) : capture.End;

            barriers.Add(new BarrierUse(capture.Start, end, capture.Absorbed, capture.Remaining, truncated));

            used += capture.Absorbed;
            wasted += capture.Remaining;
            if (capture.Remaining > 0) expiredUnspent++;
        }

        return new Computed(barriers, used, wasted, expiredUnspent);
    }

    private sealed class BarrierCapture
    {
        public int Start { get; init; }
        public int End { get; set; }
        public long Absorbed { get; set; }
        public long Remaining { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<BarrierUse> Barriers,
        long AbsorbUsed,
        long AbsorbWasted,
        int ExpiredUnspent);
}

/// <summary>
/// One Empowered Shield Slam barrier and what became of it.
/// </summary>
/// <param name="Start">When the barrier went up.</param>
/// <param name="End">When it came off, or the pull's end when <paramref name="Truncated"/>.</param>
/// <param name="Absorbed">Damage the barrier consumed.</param>
/// <param name="Remaining">Absorb still on it when it came off.</param>
/// <param name="Truncated">Whether the pull ended before the barrier did.</param>
public sealed record BarrierUse(int Start, int End, long Absorbed, long Remaining, bool Truncated)
{
    /// <summary>How long the barrier was up, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Whether the barrier came off with absorb still on it.</summary>
    public bool ExpiredUnspent => Remaining > 0;
}
