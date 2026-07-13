using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Winter's Embrace analyzer for single-target pulls: every window is scored against the
/// single-target follow-up (the detected build's spender, plus a Cold Snap or Freezing Torrent
/// finisher when the window can support it).
/// </summary>
[ForPull(PullKind.Single)]
public sealed class SingleTargetRimeCombo : BasicStComboAnalyzer
{
    private const int BaselineGlobalCooldownMs = 1500;
    private const int BaselineSpenderCastTimeMs = 1500;

    protected override StComboWindowEvaluation EvaluateWindow(WindowCapture window)
    {
        var castsInWindow = GetWindowCasts(window);
        var relevantCasts = GetRelevantCasts(castsInWindow);

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

        var result = new SingleTargetWindowEvaluation
        {
            StartTimestamp = window.StartTimestamp,
            EndTimestamp = window.EndTimestamp,
            Build = Build,
            StSpenderName = StSpenderName,
            AoeSpenderName = AoeSpenderName,
            TargetCount = window.UniqueBurstingIceTargets.Count,
            CastsInWindow = castsInWindow,
            StSpenderCount = stSpenders,
            AoeSpenderCount = relevantCasts.Count(IsAoeSpender),
            ColdSnapCount = coldSnaps,
            FreezingTorrentCount = freezingTorrents,
            ExpectedStSpenderCount = expectedStSpenders,
            ExpectsFinisher = finisherExpected,
            FirstRelevantCastName = firstRelevantCast?.Ability.Name,
            OpenedWithExpectedCast = firstRelevantCast is not null &&
                (IsStSpender(firstRelevantCast) || IsFreezingTorrent(firstRelevantCast)),
            MissingStSpenderCount = missingStSpenders,
            HasValidFinisher = hasValidFinisher,
            UsedFreezingTorrentCombo = usedFreezingTorrentCombo,
        };

        result.Successful = usedFreezingTorrentCombo || (openedWithStSpender && missingStSpenders == 0 && (!finisherExpected || hasValidFinisher));
        result.Partial = !result.Successful && (
            (stSpenders > 0 && !finisherExpected) ||
            (stSpenders > 0 && hasValidFinisher) ||
            (freezingTorrents > 0 && coldSnaps > 0));

        return result;
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

        var requiredTime = (expectedSpenders * BaselineSpenderCastTimeMs) + BaselineGlobalCooldownMs;
        return timeBudgetMs >= requiredTime;
    }

    /// <summary>A window scored against the single-target follow-up.</summary>
    public sealed class SingleTargetWindowEvaluation : StComboWindowEvaluation
    {
        /// <summary>How many single-target spender casts the window's timing could fit.</summary>
        public int ExpectedStSpenderCount { get; set; }

        /// <summary>Whether the window had enough room to also expect a finisher.</summary>
        public bool ExpectsFinisher { get; set; }

        /// <summary>Name of the first relevant cast in the window; null when no relevant spell was cast.</summary>
        public string? FirstRelevantCastName { get; set; }

        /// <summary>Whether the first relevant cast was the build's single-target spender or Freezing Torrent.</summary>
        public bool OpenedWithExpectedCast { get; set; }

        /// <summary>Expected single-target spender casts that did not happen in the window.</summary>
        public int MissingStSpenderCount { get; set; }

        /// <summary>Whether a Cold Snap or Freezing Torrent finisher was cast in the window.</summary>
        public bool HasValidFinisher { get; set; }

        /// <summary>Whether the window used the Freezing Torrent plus Cold Snap single-target variant.</summary>
        public bool UsedFreezingTorrentCombo { get; set; }
    }
}
