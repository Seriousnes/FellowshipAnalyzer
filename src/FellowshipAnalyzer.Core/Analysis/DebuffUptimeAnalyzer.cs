using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how continuously one debuff stayed on the pull's primary target. Presence windows are
/// tracked per (TargetId, TargetInstance) by an <see cref="AuraWindowLedger"/>; the primary target is
/// the one with the most active time, so transient adds never dilute the boss uptime.
/// <see cref="Uptime"/> is the primary target's active time against the pull duration, and gaps are
/// the gaps between that target's windows, so lead-in before the first application
/// lowers uptime without counting as a gap.
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on
/// the leaf, and declare the debuff's own <c>[On&lt;&gt;]</c> handlers there: apply and refresh call
/// <see cref="OpenWindow"/>, remove calls <see cref="CloseWindow"/>, and every other event the
/// debuff emits on its target (stack changes, DoT ticks) calls <see cref="ObserveTarget"/>. That is
/// what lets a window close honestly when the target stops emitting.
/// </para>
/// </summary>
public abstract class DebuffUptimeAnalyzer : Analyzer
{
    private readonly AuraWindowLedger _ledger = new();

    private Computed Result => field ??= Compute();

    /// <summary>Share of the pull (0-1) the debuff was active on the primary target.</summary>
    public double Uptime => Result.Uptime;

    /// <summary>Gaps between the primary target's windows.</summary>
    public int GapCount => Result.GapCount;

    /// <summary>Total milliseconds the debuff was off the primary target between its windows.</summary>
    public int TotalGapMs => Result.TotalGapMs;

    /// <summary>The primary target's windows, in encounter order.</summary>
    public List<AuraWindow> Windows => Result.Windows;

    /// <summary>
    /// The target every measurement above is taken against - the one with the most active time -
    /// or <c>null</c> when the debuff was never applied. A derived analyzer that tracks something else per
    /// target (stack counts, damage) reads this to scope its own state to the same target the uptime
    /// is measured on.
    /// </summary>
    public (int TargetId, int TargetInstance)? PrimaryTarget => Result.PrimaryTarget;

    /// <summary>
    /// Opens a window on <paramref name="target"/> unless one is already open, and records the event
    /// against that target. Call from apply and refresh handlers.
    /// </summary>
    protected void OpenWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Open(target, timestamp);

    /// <summary>
    /// Closes the window open on <paramref name="target"/>, if any, and records the event against that
    /// target. Call from remove handlers.
    /// </summary>
    protected void CloseWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Close(target, timestamp);

    /// <summary>
    /// Records that <paramref name="target"/> was still in the log at <paramref name="timestamp"/>,
    /// without opening or closing anything. Call from every other handler the debuff drives on its
    /// target, so a window left open at pull end can be closed at the last moment the target is
    /// known to have existed.
    /// </summary>
    protected void ObserveTarget(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Observe(target, timestamp);

    private Computed Compute()
    {
        var primary = _ledger.Build()
            .Select(entry => new Candidate(
                entry.Key.ActorId,
                entry.Key.Instance ?? 0,
                entry.Value,
                entry.Value.Sum(window => window.Duration)))
            .Where(candidate => candidate.ActiveMs > 0)
            .OrderByDescending(candidate => candidate.ActiveMs)
            .ThenBy(candidate => candidate.Windows[0].Start)
            .ThenBy(candidate => candidate.TargetId)
            .ThenBy(candidate => candidate.TargetInstance)
            .FirstOrDefault();

        if (primary is null) return new Computed(0d, 0, 0, [], null);

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
        var uptime = duration > 0 ? Math.Min(1d, primary.ActiveMs / (double)duration) : 0d;
        return new Computed(
            uptime,
            gapCount,
            totalGapMs,
            primary.Windows,
            (primary.TargetId, primary.TargetInstance));
    }

    private sealed record Candidate(int TargetId, int TargetInstance, List<AuraWindow> Windows, int ActiveMs);

    private record Computed(
        double Uptime,
        int GapCount,
        int TotalGapMs,
        List<AuraWindow> Windows,
        (int TargetId, int TargetInstance)? PrimaryTarget);
}
