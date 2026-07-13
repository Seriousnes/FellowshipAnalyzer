using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Winter's Embrace analyzer for multi-target pulls: every window is scored against the AoE
/// follow-up (two of the detected build's AoE spenders and a Cold Snap in any order).
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class AoERimeCombo : BasicStComboAnalyzer
{
    protected override StComboWindowEvaluation EvaluateWindow(WindowCapture window)
    {
        var castsInWindow = GetWindowCasts(window);
        var relevantCasts = GetRelevantCasts(castsInWindow);

        var aoeSpenders = relevantCasts.Count(IsAoeSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var hasRequiredAoeSpenders = aoeSpenders >= 2;
        var hasRequiredColdSnap = coldSnaps >= 1;

        var result = new AoeWindowEvaluation
        {
            StartTimestamp = window.StartTimestamp,
            EndTimestamp = window.EndTimestamp,
            Build = Build,
            StSpenderName = StSpenderName,
            AoeSpenderName = AoeSpenderName,
            TargetCount = window.UniqueBurstingIceTargets.Count,
            CastsInWindow = castsInWindow,
            StSpenderCount = relevantCasts.Count(IsStSpender),
            AoeSpenderCount = aoeSpenders,
            ColdSnapCount = coldSnaps,
            FreezingTorrentCount = freezingTorrents,
            HasRequiredAoeSpenders = hasRequiredAoeSpenders,
            HasRequiredColdSnap = hasRequiredColdSnap,
        };

        result.Successful = hasRequiredAoeSpenders && hasRequiredColdSnap;
        result.Partial = !result.Successful && (aoeSpenders > 0 || coldSnaps > 0 || freezingTorrents > 0);

        return result;
    }

    /// <summary>A window scored against the AoE follow-up.</summary>
    public sealed class AoeWindowEvaluation : StComboWindowEvaluation
    {
        /// <summary>Whether the window contained the expected two AoE spender casts.</summary>
        public bool HasRequiredAoeSpenders { get; set; }

        /// <summary>Whether the window contained the expected Cold Snap.</summary>
        public bool HasRequiredColdSnap { get; set; }
    }
}
