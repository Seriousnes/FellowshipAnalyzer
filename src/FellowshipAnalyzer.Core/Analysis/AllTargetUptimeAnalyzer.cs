using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how continuously one aura sat on <em>every</em> unit that carried it, rather than
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

    private IReadOnlyList<TargetCoverage> Result => field ??= Compute();

    /// <summary>Every unit that carried the aura, most-covered first.</summary>
    public IReadOnlyList<TargetCoverage> Coverage => Result;

    /// <summary>How many distinct units carried the aura at some point this pull.</summary>
    public int TargetsCovered => Result.Count;

    /// <summary>
    /// The sum of every unit's covered time, in milliseconds. A party buff on four allies for the
    /// whole pull reads four pull-lengths, so this is unit-time and not wall-clock time.
    /// </summary>
    public long TotalCoveredMs => Result.Sum(coverage => (long)coverage.CoveredMs);

    /// <summary>
    /// The mean of every covered unit's uptime share (0-1). Units that never carried the aura are not
    /// in the denominator, so this reports how well the aura held on the units it reached rather than
    /// how many it reached.
    /// </summary>
    public double AverageUptime => Result.Count > 0 ? Result.Average(coverage => coverage.Uptime) : 0;

    /// <summary>The covered time on <paramref name="unit"/>, or zero when it never carried the aura.</summary>
    public int CoveredMsOn(UnitKey unit) =>
        Result.FirstOrDefault(coverage => coverage.Unit == unit)?.CoveredMs ?? 0;

    /// <summary>Opens a window on <paramref name="target"/> unless one is already open. Call from apply and refresh handlers.</summary>
    protected void OpenWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Open(target, timestamp);

    /// <summary>Closes the window open on <paramref name="target"/>, if any. Call from remove handlers.</summary>
    protected void CloseWindow(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Close(target, timestamp);

    /// <summary>Records that <paramref name="target"/> was still in the log at <paramref name="timestamp"/>.</summary>
    protected void ObserveTarget(IHasTargetWithInstanceEvent target, int timestamp) =>
        _ledger.Observe(target, timestamp);

    private IReadOnlyList<TargetCoverage> Compute()
    {
        var duration = Pull.EndTime - Pull.StartTime;

        return
        [
            .. _ledger.Build()
                .Select(entry => new TargetCoverage(
                    entry.Key,
                    entry.Value,
                    AuraWindowLedger.CoveredMs(entry.Value),
                    duration))
                .Where(coverage => coverage.CoveredMs > 0)
                .OrderByDescending(coverage => coverage.CoveredMs)
                .ThenBy(coverage => coverage.Unit.ActorId)
        ];
    }
}

/// <summary>
/// One unit's presence windows for an aura and how much of the pull they covered.
/// </summary>
/// <param name="Unit">The unit the aura sat on.</param>
/// <param name="Windows">Its windows, in encounter order.</param>
/// <param name="CoveredMs">Milliseconds covered by <paramref name="Windows"/>, counting overlap once.</param>
/// <param name="PullDurationMs">The pull's length, the denominator for <see cref="Uptime"/>.</param>
public sealed record TargetCoverage(
    UnitKey Unit,
    IReadOnlyList<AuraWindow> Windows,
    int CoveredMs,
    int PullDurationMs)
{
    /// <summary>Share of the pull (0-1) this unit carried the aura.</summary>
    public double Uptime => PullDurationMs > 0 ? Math.Min(1d, CoveredMs / (double)PullDurationMs) : 0;
}
