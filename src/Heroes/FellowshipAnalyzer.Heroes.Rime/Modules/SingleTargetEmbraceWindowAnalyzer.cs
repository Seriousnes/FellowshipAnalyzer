using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[ForPull(PullKind.Single)]
public sealed class SingleTargetEmbraceWindowAnalyzer : WintersEmbraceWindowAnalyzer
{
    public const int BaselineGlobalCooldownMs = 1500;

    public const int BaselineSpenderCastTimeMs = 2000;

    protected override EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        IReadOnlyList<CastEvent> castsInWindow,
        IReadOnlyList<CastEvent> relevantCasts)
    {
        var stSpenders = relevantCasts.Count(IsStSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var firstRelevantCast = relevantCasts.FirstOrDefault();

        var timeBudget = Math.Max(window.EndTimestamp - window.StartTimestamp, 0);
        var expectedStSpenders = GetExpectedSpenders(timeBudget);
        var finisherExpected = ShouldExpectFinisher(timeBudget, expectedStSpenders);
        var missingStSpenders = Math.Max(expectedStSpenders - stSpenders, 0);
        var hasValidFinisher = coldSnaps > 0 || freezingTorrents > 0;
        var usedFreezingTorrentCombo = freezingTorrents > 0 && coldSnaps > 0;
        var openedWithStSpender = firstRelevantCast is not null && IsStSpender(firstRelevantCast);

        var successful = usedFreezingTorrentCombo ||
            (openedWithStSpender && missingStSpenders == 0 && (!finisherExpected || hasValidFinisher));

        var partial = !successful && (
            (stSpenders > 0 && !finisherExpected) ||
            (stSpenders > 0 && hasValidFinisher) ||
            (freezingTorrents > 0 && coldSnaps > 0));

        return new SingleTargetWindowEvaluation
        {
            ExpectedStSpenderCount = expectedStSpenders,
            ExpectsFinisher = finisherExpected,
            FirstRelevantCastName = firstRelevantCast?.Ability.Name,
            OpenedWithExpectedCast = firstRelevantCast is not null &&
                (IsStSpender(firstRelevantCast) || IsFreezingTorrent(firstRelevantCast)),
            MissingStSpenderCount = missingStSpenders,
            HasValidFinisher = hasValidFinisher,
            UsedFreezingTorrentCombo = usedFreezingTorrentCombo,
            Successful = successful,
            Partial = partial,
        };
    }

    private static int GetExpectedSpenders(int timeBudgetMs)
    {
        if (timeBudgetMs >= (BaselineSpenderCastTimeMs * 2) + BaselineGlobalCooldownMs)
            return 2;

        return timeBudgetMs >= BaselineSpenderCastTimeMs ? 1 : 0;
    }

    private static bool ShouldExpectFinisher(int timeBudgetMs, int expectedSpenders)
    {
        if (expectedSpenders <= 0)
            return false;

        return timeBudgetMs >= (expectedSpenders * BaselineSpenderCastTimeMs) + BaselineGlobalCooldownMs;
    }

    public sealed class SingleTargetWindowEvaluation : EmbraceWindowEvaluation
    {
        public int ExpectedStSpenderCount { get; init; }

        public bool ExpectsFinisher { get; init; }

        public string? FirstRelevantCastName { get; init; }

        public bool OpenedWithExpectedCast { get; init; }

        public int MissingStSpenderCount { get; init; }

        public bool HasValidFinisher { get; init; }

        public bool UsedFreezingTorrentCombo { get; init; }

        public override int SpenderCount => StSpenderCount;
        public override int ExpectedSpenderCount => ExpectedStSpenderCount;
        public override string SpenderName => StSpenderName;
    }
}
