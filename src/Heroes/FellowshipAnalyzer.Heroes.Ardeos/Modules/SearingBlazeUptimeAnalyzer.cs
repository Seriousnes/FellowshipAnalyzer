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
public sealed partial class SearingBlazeUptimeAnalyzer : Analyzer
{
    private readonly Dictionary<(int TargetId, int TargetInstance), List<DotWindow>> _windowsByTarget = [];
    private readonly Dictionary<(int TargetId, int TargetInstance), int> _openWindowStarts = [];

    private IReadOnlyList<DotWindow> _windows = [];

    /// <summary>Share of the pull (0-1) the primary target spent carrying the DoT.</summary>
    public double Uptime { get; private set; }

    /// <summary>Uncovered stretches between the primary target's windows.</summary>
    public int GapCount { get; private set; }

    /// <summary>Total milliseconds the DoT was down between the primary target's windows.</summary>
    public int TotalGapMs { get; private set; }

    /// <summary>The primary target's DoT windows, in encounter order.</summary>
    public IReadOnlyList<DotWindow> Windows => _windows;

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

    /// <summary>Closes still-open windows at pull end and finalizes the primary target's uptime and gaps.</summary>
    public override void OnPullEnd()
    {
        var pull = Owner.CurrentPull;
        if (pull is null) return;

        foreach (var (key, start) in _openWindowStarts)
            GetTargetWindows(key).Add(new DotWindow(start, pull.EndTime));
        _openWindowStarts.Clear();

        List<DotWindow>? primary = null;
        var primaryCovered = 0;
        foreach (var windows in _windowsByTarget.Values)
        {
            var covered = windows.Sum(w => w.End - w.Start);
            if (covered <= primaryCovered) continue;

            primary = windows;
            primaryCovered = covered;
        }

        if (primary is null) return;

        _windows = primary;
        for (var i = 1; i < primary.Count; i++)
        {
            var gap = primary[i].Start - primary[i - 1].End;
            if (gap <= 0) continue;

            GapCount++;
            TotalGapMs += gap;
        }

        var duration = pull.EndTime - pull.StartTime;
        Uptime = duration > 0 ? Math.Min(1d, primaryCovered / (double)duration) : 0d;
    }

    /// <summary>One continuous stretch of the DoT on a target, in absolute timestamps.</summary>
    public sealed record DotWindow(int Start, int End);
}
