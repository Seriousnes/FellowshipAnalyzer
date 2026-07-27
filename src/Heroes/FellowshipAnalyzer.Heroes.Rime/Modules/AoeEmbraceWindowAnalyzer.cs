using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[ForPull(PullKind.Multi)]
public sealed class AoeEmbraceWindowAnalyzer : WintersEmbraceWindowAnalyzer
{
    public const int RequiredAoeSpenders = 2;

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

    public sealed class AoeWindowEvaluation : EmbraceWindowEvaluation
    {
        public bool HasRequiredAoeSpenders { get; init; }

        public bool HasRequiredColdSnap { get; init; }

        public override int SpenderCount => AoeSpenderCount;
        public override int ExpectedSpenderCount => RequiredAoeSpenders;
        public override string SpenderName => AoeSpenderName;
    }
}
