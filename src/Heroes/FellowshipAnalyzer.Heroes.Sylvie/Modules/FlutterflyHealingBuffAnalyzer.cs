using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// The two things that scale flutterfly healing: how many Prickly Vines were alive, each worth three
/// percent, and how much of the pull Safe Haven's forty percent healing-taken window covered.
/// <para>
/// A vine is not an entity in the log - it has no summon and no despawn event, and its damage arrives
/// under Sylvie's own source id - so the live count is modelled from the casts against the twenty-seven
/// second lifetime in the game data. That is an inference, not an observation, and a vine killed early
/// or cast out of range still counts here.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FlutterflyHealingBuffAnalyzer : Analyzer
{
    private readonly List<int> _vineCasts = [];
    private readonly List<(int TargetId, AuraWindow Window)> _safeHavenWindows = [];
    private readonly Dictionary<int, int> _openSafeHaven = [];

    private Computed Result => field ??= Compute();

    /// <summary>Prickly Vines cast this pull.</summary>
    public int VineCasts => _vineCasts.Count;

    /// <summary>Time-weighted mean of the live vine count across the pull.</summary>
    public double AverageLiveVines => Result.AverageVines;

    /// <summary>The most vines modelled alive at once this pull.</summary>
    public int PeakLiveVines => Result.PeakVines;

    /// <summary>Milliseconds with no vine alive at all, where the flutterflies got no vine bonus.</summary>
    public int NoVineMs => Result.NoVineMs;

    /// <summary>Share (0-1) of the pull with no vine alive.</summary>
    public double NoVineShare => PullDurationMs > 0 ? NoVineMs / (double)PullDurationMs : 0;

    /// <summary>
    /// The mean flutterfly healing multiplier the vines were worth, time-weighted: one plus three
    /// percent per live vine.
    /// </summary>
    public double AverageVineMultiplier => 1 + (AverageLiveVines * SylvieKit.FlutterflyHealPerVine);

    /// <summary>Safe Havens cast this pull.</summary>
    public int SafeHavenCasts { get; private set; }

    /// <summary>Milliseconds of the pull covered by at least one ally standing in a Safe Haven.</summary>
    public int SafeHavenCoveredMs => Result.SafeHavenMs;

    /// <summary>Share (0-1) of the pull with a Safe Haven window open on somebody.</summary>
    public double SafeHavenUptime => PullDurationMs > 0 ? Math.Min(1d, SafeHavenCoveredMs / (double)PullDurationMs) : 0;

    /// <summary>Ally-milliseconds of Safe Haven coverage, counting each ally in the circle separately.</summary>
    public long SafeHavenAllyMs => Result.SafeHavenAllyMs;

    /// <summary>Distinct allies who stood in a Safe Haven this pull.</summary>
    public int SafeHavenAllies => Result.SafeHavenAllies;

    /// <summary>The pull's length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.PricklyVine))]
    private void OnVineCast(CastEvent castEvent) => _vineCasts.Add(castEvent.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHaven))]
    private void OnSafeHavenCast(CastEvent castEvent) => SafeHavenCasts++;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnSafeHavenApplied(ApplyBuffEvent buffEvent) =>
        _openSafeHaven.TryAdd(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnSafeHavenRefreshed(RefreshBuffEvent buffEvent) =>
        _openSafeHaven.TryAdd(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnSafeHavenRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSafeHaven.Remove(buffEvent.TargetId, out var start))
            _safeHavenWindows.Add((buffEvent.TargetId, new AuraWindow(start, buffEvent.Timestamp)));
    }

    private Computed Compute()
    {
        var covered = new List<(int TargetId, AuraWindow Window)>(_safeHavenWindows);
        foreach (var (targetId, start) in _openSafeHaven)
            covered.Add((targetId, new AuraWindow(start, Pull.EndTime)));

        var windows = covered.Select(entry => entry.Window).ToArray();
        var (average, peak, noVineMs) = MeasureVines();

        return new Computed(
            average,
            peak,
            noVineMs,
            AuraWindowLedger.CoveredMs(windows),
            windows.Sum(window => (long)window.Duration),
            covered.Select(entry => entry.TargetId).Distinct().Count());
    }

    private (double Average, int Peak, int NoVineMs) MeasureVines()
    {
        if (PullDurationMs == 0) return (0, 0, 0);

        var changes = new List<(int Timestamp, int Delta)>(_vineCasts.Count * 2);
        foreach (var cast in _vineCasts)
        {
            changes.Add((cast + SylvieKit.PricklyVineSpawnMs, 1));
            changes.Add((cast + SylvieKit.PricklyVineSpawnMs + SylvieKit.PricklyVineDurationMs, -1));
        }

        changes.Sort(static (left, right) => left.Timestamp.CompareTo(right.Timestamp));

        var live = 0;
        var peak = 0;
        var noVineMs = 0;
        long weighted = 0;
        var cursor = Pull.StartTime;

        foreach (var (timestamp, delta) in changes)
        {
            var at = Math.Clamp(timestamp, Pull.StartTime, Pull.EndTime);
            var elapsed = at - cursor;
            if (elapsed > 0)
            {
                weighted += (long)live * elapsed;
                if (live == 0) noVineMs += elapsed;
                cursor = at;
            }

            live += delta;
            peak = Math.Max(peak, live);
        }

        var tail = Pull.EndTime - cursor;
        if (tail > 0)
        {
            weighted += (long)live * tail;
            if (live == 0) noVineMs += tail;
        }

        return (weighted / (double)PullDurationMs, peak, noVineMs);
    }

    private sealed record Computed(
        double AverageVines,
        int PeakVines,
        int NoVineMs,
        int SafeHavenMs,
        long SafeHavenAllyMs,
        int SafeHavenAllies);
}
