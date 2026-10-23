using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[ForPull(PullKind.Single)]
public sealed class SingleTargetEmbraceAnalyzer : WintersEmbraceAnalyzer
{
    public const int BaselineGlobalCooldownMs = 1500;

    /// <summary>Cast time Greater Glacial Blast adds to Glacial Blast.</summary>
    public const int GreaterGlacialBlastCastTimeIncreaseMs = 500;

    /// <summary>
    /// Unhasted time one single-target spender occupies: the build's spender cast time, never below the
    /// global cooldown, plus <see cref="GreaterGlacialBlastCastTimeIncreaseMs"/> for Glacial Blast when
    /// Greater Glacial Blast is talented.
    /// </summary>
    public int SpenderCastTimeMs
    {
        get
        {
            if (Build == RimeBuild.IcyTalons)
                return CastTimeMs(RimeSpells.TalonStrike);

            var castTimeMs = CastTimeMs(RimeSpells.GlacialBlast);
            return Owner.SelectedCombatant.HasTalent(RimeTalents.GreaterGlacialBlast)
                ? castTimeMs + GreaterGlacialBlastCastTimeIncreaseMs
                : castTimeMs;
        }
    }

    protected override EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var stSpenders = relevantCasts.Count(IsStSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var firstRelevantCast = relevantCasts.FirstOrDefault();

        var spenderCastTimeMs = SpenderCastTimeMs;
        var timeBudget = Math.Max(window.EndTimestamp - window.StartTimestamp, 0);
        var expectedStSpenders = GetExpectedSpenders(timeBudget, spenderCastTimeMs);
        var finisherExpected = ShouldExpectFinisher(timeBudget, expectedStSpenders, spenderCastTimeMs);
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

    private static int CastTimeMs(Spell spell) =>
        Math.Max(BaselineGlobalCooldownMs, (int)Math.Round((spell.CastDuration ?? 0) * 1000));

    private static int GetExpectedSpenders(int timeBudgetMs, int spenderCastTimeMs)
    {
        if (timeBudgetMs >= (spenderCastTimeMs * 2) + BaselineGlobalCooldownMs)
            return 2;

        return timeBudgetMs >= spenderCastTimeMs ? 1 : 0;
    }

    private static bool ShouldExpectFinisher(int timeBudgetMs, int expectedSpenders, int spenderCastTimeMs)
    {
        if (expectedSpenders <= 0)
            return false;

        return timeBudgetMs >= (expectedSpenders * spenderCastTimeMs) + BaselineGlobalCooldownMs;
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
