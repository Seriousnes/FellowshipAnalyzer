using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Winter's Embrace analyzer for single-target pulls. The window is scored against the single-target
/// follow-up: open on the detected build's Winter Orb spender, fit as many spender casts as the
/// window's own length can carry, and finish with Cold Snap or Freezing Torrent when there is room
/// left for one.
/// </summary>
/// <remarks>
/// How many spenders a window should carry is derived from the window's measured length rather than
/// assumed, because talents extend Winter's Embrace: a spender occupies
/// <see cref="BaselineSpenderCastTimeMs"/> and each further cast needs a
/// <see cref="BaselineGlobalCooldownMs"/> global cooldown on top. Freezing Torrent into Cold Snap is
/// the accepted alternative opening, so a window carrying both counts as clean regardless of the
/// spender arithmetic.
/// </remarks>
[ForPull(PullKind.Single)]
public sealed class SingleTargetEmbraceWindowAnalyzer : WintersEmbraceWindowAnalyzer
{
    /// <summary>Baseline global cooldown in milliseconds, the gap between consecutive casts in a window.</summary>
    public const int BaselineGlobalCooldownMs = 1500;

    /// <summary>Baseline single-target Winter Orb spender cast time in milliseconds.</summary>
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

    /// <summary>A window scored against the single-target follow-up.</summary>
    public sealed class SingleTargetWindowEvaluation : EmbraceWindowEvaluation
    {
        /// <summary>How many single-target spender casts the window's measured length could fit.</summary>
        public int ExpectedStSpenderCount { get; init; }

        /// <summary>Whether the window had enough room left to also expect a finisher.</summary>
        public bool ExpectsFinisher { get; init; }

        /// <summary>Name of the first relevant cast in the window; null when no relevant spell was cast.</summary>
        public string? FirstRelevantCastName { get; init; }

        /// <summary>Whether the first relevant cast was the build's single-target spender or Freezing Torrent.</summary>
        public bool OpenedWithExpectedCast { get; init; }

        /// <summary>Expected single-target spender casts that did not happen in the window.</summary>
        public int MissingStSpenderCount { get; init; }

        /// <summary>Whether a Cold Snap or Freezing Torrent finisher was cast in the window.</summary>
        public bool HasValidFinisher { get; init; }

        /// <summary>Whether the window used the Freezing Torrent plus Cold Snap single-target variant.</summary>
        public bool UsedFreezingTorrentCombo { get; init; }

        public override int SpenderCount => StSpenderCount;
        public override int ExpectedSpenderCount => ExpectedStSpenderCount;
        public override string SpenderName => StSpenderName;
    }
}
