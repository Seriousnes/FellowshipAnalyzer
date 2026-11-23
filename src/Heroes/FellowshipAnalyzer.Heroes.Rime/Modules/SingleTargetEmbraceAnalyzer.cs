using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;
using RimeTalentTuning = FellowshipAnalyzer.Core.Common.Spells.Rime.TalentTuning;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[ForPull(PullKind.Single)]
[Dependency<Haste>]
public sealed partial class SingleTargetEmbraceAnalyzer : WintersEmbraceAnalyzer
{
    /// <summary>
    /// Cast time in milliseconds Greater Glacial Blast adds to Glacial Blast and Talon Strike, read from the
    /// talent's tuning.
    /// </summary>
    public static readonly int GreaterGlacialBlastCastTimeIncreaseMs =
        (int)(RimeTalentTuning.GreaterGlacialBlast.DamageIncreaseAndCastTimeIncrease_CastTimeIncreaseDuration * 1000);

    private Spell Spender => Build == RimeBuild.IcyTalons ? RimeSpells.TalonStrike : RimeSpells.GlacialBlast;

    protected override double HasteNow => Haste.Current;

    /// <summary>
    /// Time one single-target spender occupies at the Haste of the first Winter's Embrace window: its cast time,
    /// never below its global cooldown, with both shortened by Haste where the spell data says so. Greater
    /// Glacial Blast lengthens the cast time before Haste applies. Haste at the analyzer's read time stands in
    /// when no window opened.
    /// </summary>
    public int SpenderCastTimeMs => SpenderCastTimeMsAt(Windows.Count > 0 ? Windows[0].HasteAtOpen : Haste.Current);

    /// <summary>Time one single-target spender occupies at <paramref name="haste"/>, as a fraction.</summary>
    public int SpenderCastTimeMsAt(double haste) =>
        RoundMs(Math.Max(GlobalCooldownMs(Spender, haste), CastMs(Spender, haste)));

    protected override EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var stSpenders = relevantCasts.Count(IsStSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var firstRelevantCast = relevantCasts.FirstOrDefault();

        var spenderCastTimeMs = SpenderCastTimeMsAt(window.HasteAtOpen);
        var globalCooldownMs = RoundMs(GlobalCooldownMs(Spender, window.HasteAtOpen));
        var timeBudget = Math.Max(window.EndTimestamp - window.StartTimestamp, 0);
        var expectedStSpenders = GetExpectedSpenders(timeBudget, spenderCastTimeMs, globalCooldownMs);
        var finisherExpected = ShouldExpectFinisher(timeBudget, expectedStSpenders, spenderCastTimeMs, globalCooldownMs);
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

    private static int RoundMs(double milliseconds) => (int)Math.Round(milliseconds, MidpointRounding.AwayFromZero);

    private static double GlobalCooldownMs(Spell spell, double haste) =>
        (spell.GlobalCooldown ?? 0) * 1000 / (spell.GlobalCooldownScalesWithHaste == true ? 1 + haste : 1);

    private double CastMs(Spell spell, double haste)
    {
        var increaseMs = Owner.SelectedCombatant.HasTalent(RimeTalents.GreaterGlacialBlast)
            ? GreaterGlacialBlastCastTimeIncreaseMs
            : 0;

        return (((spell.CastDuration ?? 0) * 1000) + increaseMs) / (spell.CastTimeScalesWithHaste == true ? 1 + haste : 1);
    }

    private static int GetExpectedSpenders(int timeBudgetMs, int spenderCastTimeMs, int globalCooldownMs)
    {
        if (timeBudgetMs >= (spenderCastTimeMs * 2) + globalCooldownMs)
            return 2;

        return timeBudgetMs >= spenderCastTimeMs ? 1 : 0;
    }

    private static bool ShouldExpectFinisher(int timeBudgetMs, int expectedSpenders, int spenderCastTimeMs, int globalCooldownMs)
    {
        if (expectedSpenders <= 0)
            return false;

        return timeBudgetMs >= (expectedSpenders * spenderCastTimeMs) + globalCooldownMs;
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
