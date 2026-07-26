using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how continuously one debuff stayed on the pull's primary target. Presence windows are
/// tracked per (TargetId, TargetInstance); the primary target is the one carrying the most covered
/// time, so transient adds never dilute the boss reading. <see cref="Uptime"/> is the primary
/// target's covered time against the pull duration, and gaps are the uncovered stretches between
/// that target's windows, so lead-in before the first application lowers uptime without counting as
/// a gap.
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on
/// the leaf, and declare the debuff's own <c>[On&lt;&gt;]</c> handlers there: apply and refresh call
/// <see cref="OpenWindow"/>, remove calls <see cref="CloseWindow"/>, and every other event the
/// debuff emits on its target (stack changes, DoT ticks) calls <see cref="ObserveTarget"/>. Those
/// observations are what let a window close honestly when the target stops emitting.
/// </para>
/// </summary>
public abstract class DebuffUptimeAnalyzer : Analyzer
{
    private readonly Dictionary<(int TargetId, int TargetInstance), List<AuraWindow>> _windowsByTarget = [];
    private readonly Dictionary<(int TargetId, int TargetInstance), int> _openWindowStarts = [];
    private readonly Dictionary<(int TargetId, int TargetInstance), int> _lastObserved = [];

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Share of the pull (0-1) the primary target spent carrying the debuff.</summary>
    public double Uptime => Result.Uptime;

    /// <summary>Uncovered stretches between the primary target's windows.</summary>
    public int GapCount => Result.GapCount;

    /// <summary>Total milliseconds the debuff was off the primary target between its windows.</summary>
    public int TotalGapMs => Result.TotalGapMs;

    /// <summary>The primary target's windows, in encounter order.</summary>
    public IReadOnlyList<AuraWindow> Windows => Result.Windows;

    /// <summary>
    /// Opens a window on <paramref name="target"/> unless one is already open, and records the event
    /// as an observation of that target. Call from apply and refresh handlers.
    /// </summary>
    protected void OpenWindow(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var key = Key(target);
        _lastObserved[key] = timestamp;
        _openWindowStarts.TryAdd(key, timestamp);
    }

    /// <summary>
    /// Closes the window open on <paramref name="target"/>, if any, and records the event as an
    /// observation of that target. Call from remove handlers.
    /// </summary>
    protected void CloseWindow(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var key = Key(target);
        _lastObserved[key] = timestamp;
        if (!_openWindowStarts.Remove(key, out var start)) return;

        WindowsFor(key).Add(new AuraWindow(start, timestamp));
    }

    /// <summary>
    /// Records that <paramref name="target"/> was still in the log at <paramref name="timestamp"/>,
    /// without opening or closing anything. Call from every other handler the debuff drives on its
    /// target, so a window left open at pull end can be closed at the last moment the target is
    /// known to have existed.
    /// </summary>
    protected void ObserveTarget(IHasTargetWithInstanceEvent target, int timestamp) =>
        _lastObserved[Key(target)] = timestamp;

    private static (int TargetId, int TargetInstance) Key(IHasTargetWithInstanceEvent target) =>
        (target.TargetId, target.TargetInstance ?? 0);

    private List<AuraWindow> WindowsFor((int TargetId, int TargetInstance) key)
    {
        if (_windowsByTarget.TryGetValue(key, out var windows)) return windows;

        windows = [];
        _windowsByTarget[key] = windows;
        return windows;
    }

    /// <summary>
    /// Closes still-open windows and elects the primary target's uptime and gaps. A window still
    /// open at pull end closes at that target's last observed event rather than at the pull
    /// boundary: a non-selected enemy that dies is never logged as dying, it simply stops emitting,
    /// so stretching its window to the boundary would let a two-second add out-cover the boss and
    /// erase the boss's real gaps. The genuinely long-lived target keeps emitting to the end of the
    /// pull and so wins the election on its own coverage. Ties break on the earliest first window,
    /// then on the target key, so the reading never depends on dictionary order. Computed once, on
    /// first read.
    /// </summary>
    private Computed Compute()
    {
        var windowsByTarget = new Dictionary<(int TargetId, int TargetInstance), List<AuraWindow>>();
        foreach (var (key, windows) in _windowsByTarget)
            windowsByTarget[key] = [.. windows];
        foreach (var (key, start) in _openWindowStarts)
        {
            if (!windowsByTarget.TryGetValue(key, out var list))
                windowsByTarget[key] = list = [];
            list.Add(new AuraWindow(start, Math.Max(start, _lastObserved.GetValueOrDefault(key))));
        }

        var primary = windowsByTarget
            .Select(entry => new Candidate(
                entry.Key.TargetId,
                entry.Key.TargetInstance,
                entry.Value,
                entry.Value.Sum(window => window.Duration)))
            .Where(candidate => candidate.Covered > 0)
            .OrderByDescending(candidate => candidate.Covered)
            .ThenBy(candidate => candidate.Windows[0].Start)
            .ThenBy(candidate => candidate.TargetId)
            .ThenBy(candidate => candidate.TargetInstance)
            .FirstOrDefault();

        if (primary is null) return new Computed(0d, 0, 0, []);

        var gapCount = 0;
        var totalGapMs = 0;
        for (var i = 1; i < primary.Windows.Count; i++)
        {
            var gap = primary.Windows[i].Start - primary.Windows[i - 1].End;
            if (gap <= 0) continue;

            gapCount++;
            totalGapMs += gap;
        }

        var duration = Pull.EndTime - Pull.StartTime;
        var uptime = duration > 0 ? Math.Min(1d, primary.Covered / (double)duration) : 0d;
        return new Computed(uptime, gapCount, totalGapMs, primary.Windows);
    }

    private sealed record Candidate(int TargetId, int TargetInstance, List<AuraWindow> Windows, int Covered);

    private readonly record struct Computed(double Uptime, int GapCount, int TotalGapMs, IReadOnlyList<AuraWindow> Windows);
}
