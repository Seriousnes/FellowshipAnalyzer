using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how continuously one aura was active on <em>every</em> unit it was applied to, rather than
/// collapsing to a single primary target the way <see cref="DebuffUptimeAnalyzer"/> does. Both share
/// the same <see cref="AuraWindowLedger"/> bookkeeping; this one keeps a row per unit, which is what
/// a raid-wide buff needs - a party buff has no primary target and reporting one would hide the ally
/// it missed.
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on
/// the leaf, and declare the aura's own <c>[On&lt;&gt;]</c> handlers there: apply and refresh call
/// <see cref="OpenWindow"/>, remove calls <see cref="CloseWindow"/>, and anything else the aura
/// emits on its target calls <see cref="ObserveTarget"/>.
/// </para>
/// </summary>
public abstract class AllTargetUptimeAnalyzer : Analyzer
{
    private readonly AuraWindowLedger _ledger = new();

    private List<TargetUptime> Result => field ??= Compute();

    /// <summary>Every unit the aura was applied to, longest active time first.</summary>
    public List<TargetUptime> TargetUptimes => Result;

    /// <summary>
    /// The sum of every unit's active time, in milliseconds. A party buff on four allies for the
    /// whole pull reads four pull-lengths, so this is unit-time and not wall-clock time.
    /// </summary>
    public long TotalActiveMs => Result.Sum(target => (long)target.ActiveMs);

    /// <summary>
    /// The mean uptime share (0-1) across <see cref="TargetUptimes"/>. Units the aura was never
    /// applied to are not in the denominator.
    /// </summary>
    public double AverageUptime => Result.Count > 0 ? Result.Average(target => target.Uptime) : 0;

    /// <summary>The active time on <paramref name="unit"/>, or zero when the aura was never applied to it.</summary>
    public int ActiveMsOn(UnitKey unit) =>
        Result.FirstOrDefault(target => target.Unit == unit)?.ActiveMs ?? 0;

    /// <summary>Opens a window on <paramref name="target"/> unless one is already open. Call from apply and refresh handlers.</summary>
    protected void OpenWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Open(target, timestamp);

    /// <summary>Closes the window open on <paramref name="target"/>, if any. Call from remove handlers.</summary>
    protected void CloseWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Close(target, timestamp);

    /// <summary>Records that <paramref name="target"/> was still in the log at <paramref name="timestamp"/>.</summary>
    protected void ObserveTarget(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Observe(target, timestamp);

    private List<TargetUptime> Compute()
    {
        var duration = Pull.EndTime - Pull.StartTime;

        return
        [
            .. _ledger.Build()
                .Select(entry => new TargetUptime(
                    entry.Key,
                    entry.Value,
                    AuraWindowLedger.ActiveMs(entry.Value),
                    duration))
                .Where(target => target.ActiveMs > 0)
                .OrderByDescending(target => target.ActiveMs)
                .ThenBy(target => target.Unit.ActorId)
        ];
    }
}

/// <summary>
/// One unit's aura windows and the active time inside them.
/// </summary>
/// <param name="Unit">The unit the aura was active on.</param>
/// <param name="Windows">Its windows, in encounter order.</param>
/// <param name="ActiveMs">Milliseconds inside <paramref name="Windows"/>, counting overlap once.</param>
/// <param name="PullDurationMs">The pull's length, the denominator for <see cref="Uptime"/>.</param>
public sealed record TargetUptime(
    UnitKey Unit,
    List<AuraWindow> Windows,
    int ActiveMs,
    int PullDurationMs)
{
    /// <summary>Share of the pull (0-1) the aura was active on this unit.</summary>
    public double Uptime => PullDurationMs > 0 ? Math.Min(1d, ActiveMs / (double)PullDurationMs) : 0;
}
