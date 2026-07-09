using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Scores how well Ardeos spread Searing Blaze across a multi-target pull. Each enemy that took the
/// Searing Blaze DoT from the player is counted once by (TargetId, TargetInstance); coverage is that
/// distinct count against the pull's enemy roster (<see cref="Pull.TargetCount"/>). Applies and
/// refreshes both mark a target debuffed and the HashSet dedupes, so a refresh never counts as a new
/// enemy. Counting is monotonic, so enemies that die carrying the DoT (which never log a remove) do
/// not drift the metric.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed partial class SearingBlazeSpreadAnalyzer : Analyzer<SearingBlazeSpreadReport>
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _debuffedTargets = [];
    private int _totalApplications;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnApplied(ApplyDebuffEvent e)
    {
        _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));
        _totalApplications++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRefreshed(RefreshDebuffEvent e)
        => _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));

    /// <summary>Projects the distinct-target coverage accumulated across the closing pull.</summary>
    public override SearingBlazeSpreadReport OnPullEnd()
    {
        var distinct = _debuffedTargets.Count;
        var targetCount = Owner.CurrentPull?.TargetCount ?? 0;
        var denominator = targetCount > 0 ? targetCount : Math.Max(distinct, UnknownRosterReference);
        var coverage = denominator == 0 ? 0d : Math.Min(1d, distinct / (double)denominator);
        var score = distinct == 0 ? 0 : (int)Math.Round(coverage * 100);

        var summary = distinct == 0
            ? "Searing Blaze was not spread on this pull."
            : targetCount > 0
                ? $"Searing Blaze covered {distinct}/{targetCount} enemies on this pull."
                : $"Searing Blaze reached {distinct} enemies on this pull.";

        var scoreCard = new AnalyzerScoreCard(
            "Searing Blaze Spread",
            score,
            summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        var findings = BuildFindings(distinct, targetCount, score);

        return new SearingBlazeSpreadReport(scoreCard, distinct, targetCount, coverage, _totalApplications, findings);
    }

    private static List<ArdeosFinding> BuildFindings(int distinct, int targetCount, int score)
    {
        if (distinct == 0)
            return [new ArdeosFinding("info", "No Searing Blaze applications were detected on this AoE pull.")];

        if (targetCount <= 0)
            return [new ArdeosFinding("info",
                $"Searing Blaze reached {distinct} enemies (the enemy roster was not reported, so coverage is estimated).")];

        if (score >= 75)
            return [new ArdeosFinding("info",
                $"Searing Blaze covered {distinct} of {targetCount} enemies on this pull.")];

        var severity = score >= 50 ? "warning" : "major";
        return [new ArdeosFinding(severity,
            $"Searing Blaze reached only {distinct} of {targetCount} enemies — keep the DoT spread across the pack.")];
    }
}
