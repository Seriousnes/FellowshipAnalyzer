using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Winter's Embrace analyzer for multi-target pulls. The window is scored against the AoE follow-up:
/// two of the detected build's AoE Winter Orb spenders plus a Cold Snap, in any order.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class AoeEmbraceWindowAnalyzer : WintersEmbraceWindowAnalyzer
{
    /// <summary>AoE Winter Orb spender casts a clean AoE window carries.</summary>
    public const int RequiredAoeSpenders = 2;

    /// <summary>Cold Snap casts a clean AoE window carries.</summary>
    public const int RequiredColdSnaps = 1;

    protected override EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        IReadOnlyList<CastEvent> castsInWindow,
        IReadOnlyList<CastEvent> relevantCasts)
    {
        var aoeSpenders = relevantCasts.Count(IsAoeSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);

        var hasRequiredAoeSpenders = aoeSpenders >= RequiredAoeSpenders;
        var hasRequiredColdSnap = coldSnaps >= RequiredColdSnaps;

        var successful = hasRequiredAoeSpenders && hasRequiredColdSnap;

        return new AoeWindowEvaluation
        {
            HasRequiredAoeSpenders = hasRequiredAoeSpenders,
            HasRequiredColdSnap = hasRequiredColdSnap,
            Successful = successful,
            Partial = !successful && (aoeSpenders > 0 || coldSnaps > 0 || freezingTorrents > 0),
        };
    }

    /// <summary>A window scored against the AoE follow-up.</summary>
    public sealed class AoeWindowEvaluation : EmbraceWindowEvaluation
    {
        /// <summary>Whether the window contained the expected two AoE spender casts.</summary>
        public bool HasRequiredAoeSpenders { get; init; }

        /// <summary>Whether the window contained the expected Cold Snap.</summary>
        public bool HasRequiredColdSnap { get; init; }

        public override int SpenderCount => AoeSpenderCount;
        public override int ExpectedSpenderCount => RequiredAoeSpenders;
        public override string SpenderName => AoeSpenderName;
    }
}
