using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how well Ardeos spread Searing Blaze across a multi-target pull. Each enemy that took the
/// Searing Blaze DoT from the player is counted once by (TargetId, TargetInstance); coverage is that
/// distinct count against the pull's enemy roster (<see cref="Pull.TargetCount"/>). Applies and
/// refreshes both mark a target debuffed and the HashSet dedupes, so a refresh never counts as a new
/// enemy. Counting is monotonic, so enemies that die carrying the DoT (which never log a remove) do
/// not drift the metric.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed partial class SearingBlazeSpreadAnalyzer : Analyzer, ISearingBlazeAnalyzer
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _debuffedTargets = [];

    /// <summary>Distinct enemies that took the Searing Blaze DoT from the player.</summary>
    public int DistinctTargets => _debuffedTargets.Count;

    /// <summary>The pull's reported enemy roster size; zero when the roster was not reported.</summary>
    public int TargetCount { get; private set; }

    /// <summary>
    /// Share of the roster covered (0-1). When the roster is unknown, coverage is estimated against
    /// a reference pack size, so it stays comparable across pulls.
    /// </summary>
    public double Coverage { get; private set; }

    /// <summary>Fresh Searing Blaze applications during the pull (refreshes excluded).</summary>
    public int TotalApplications { get; private set; }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnApplied(ApplyDebuffEvent e)
    {
        _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));
        TotalApplications++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRefreshed(RefreshDebuffEvent e)
        => _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));

    /// <summary>Finalizes the distinct-target coverage accumulated across the closing pull.</summary>
    public override void OnPullEnd()
    {
        var distinct = _debuffedTargets.Count;
        TargetCount = Owner.CurrentPull?.TargetCount ?? 0;
        var denominator = TargetCount > 0 ? TargetCount : Math.Max(distinct, UnknownRosterReference);
        Coverage = denominator == 0 ? 0d : Math.Min(1d, distinct / (double)denominator);
    }
}
