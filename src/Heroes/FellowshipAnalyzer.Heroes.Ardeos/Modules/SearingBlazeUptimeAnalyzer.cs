using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how continuously Ardeos kept the Searing Blaze DoT rolling on a boss pull. Windows
/// are tracked per (TargetId, TargetInstance) from apply/refresh/remove events, with a still-open
/// window closed at pull end; the primary target is the one carrying the most DoT time, so
/// transient adds never dilute the boss reading. Uptime is the primary target's covered time
/// against the pull duration. Gaps are the uncovered stretches between that target's windows;
/// lead-in before the first application lowers uptime without counting as a gap.
/// </summary>
[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class SearingBlazeUptimeAnalyzer : Analyzer, ISearingBlazeAnalyzer
{
    private readonly Dictionary<(int TargetId, int TargetInstance), List<DotWindow>> _windowsByTarget = [];
    private readonly Dictionary<(int TargetId, int TargetInstance), int> _openWindowStarts = [];

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Share of the pull (0-1) the primary target spent carrying the DoT.</summary>
    public double Uptime => Result.Uptime;

    /// <summary>Uncovered stretches between the primary target's windows.</summary>
    public int GapCount => Result.GapCount;

    /// <summary>Total milliseconds the DoT was down between the primary target's windows.</summary>
    public int TotalGapMs => Result.TotalGapMs;

    /// <summary>The primary target's DoT windows, in encounter order.</summary>
    public IReadOnlyList<DotWindow> Windows => Result.Windows;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnApplied(ApplyDebuffEvent e) => OpenWindow(e);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRefreshed(RefreshDebuffEvent e) => OpenWindow(e);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRemoved(RemoveDebuffEvent e) => CloseWindow(e);

    private void OpenWindow(BuffEvent e)
    {
        var key = (e.TargetId, e.TargetInstance ?? 0);
        _openWindowStarts.TryAdd(key, e.Timestamp);
    }

    private void CloseWindow(BuffEvent e)
    {
        var key = (e.TargetId, e.TargetInstance ?? 0);
        if (!_openWindowStarts.Remove(key, out var start)) return;

        GetTargetWindows(key).Add(new DotWindow(start, e.Timestamp));
    }

    private List<DotWindow> GetTargetWindows((int TargetId, int TargetInstance) key)
    {
        if (_windowsByTarget.TryGetValue(key, out var windows)) return windows;

        windows = [];
        _windowsByTarget[key] = windows;
        return windows;
    }

    /// <summary>
    /// Closes still-open windows at the pull boundary and picks the primary target's uptime and gaps.
    /// A window still open at pull end is a target that died without a logged Remove; the boss (the
    /// max-coverage target) dies at the pull's end time. Computed once, on first read.
    /// </summary>
    private Computed Compute()
    {
        var windowsByTarget = new Dictionary<(int TargetId, int TargetInstance), List<DotWindow>>();
        foreach (var (key, windows) in _windowsByTarget)
            windowsByTarget[key] = [.. windows];
        foreach (var (key, start) in _openWindowStarts)
        {
            if (!windowsByTarget.TryGetValue(key, out var list))
                windowsByTarget[key] = list = [];
            list.Add(new DotWindow(start, Pull.EndTime));
        }

        List<DotWindow>? primary = null;
        var primaryCovered = 0;
        foreach (var windows in windowsByTarget.Values)
        {
            var covered = windows.Sum(w => w.End - w.Start);
            if (covered <= primaryCovered) continue;

            primary = windows;
            primaryCovered = covered;
        }

        if (primary is null) return new Computed(0d, 0, 0, []);

        var gapCount = 0;
        var totalGapMs = 0;
        for (var i = 1; i < primary.Count; i++)
        {
            var gap = primary[i].Start - primary[i - 1].End;
            if (gap <= 0) continue;

            gapCount++;
            totalGapMs += gap;
        }

        var duration = Pull.EndTime - Pull.StartTime;
        var uptime = duration > 0 ? Math.Min(1d, primaryCovered / (double)duration) : 0d;
        return new Computed(uptime, gapCount, totalGapMs, primary);
    }

    private readonly record struct Computed(double Uptime, int GapCount, int TotalGapMs, IReadOnlyList<DotWindow> Windows);

    /// <summary>One continuous stretch of the DoT on a target, in absolute timestamps.</summary>
    public sealed record DotWindow(int Start, int End);
}
