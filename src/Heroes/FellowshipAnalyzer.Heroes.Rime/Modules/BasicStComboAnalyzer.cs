using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

using System.Text;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/**
 * While Bursting Ice is active on an enemy you gain Winter's Embrace, causing you to deal 20% more damage.
 * 
 * Winter's Embrace does not affect Bursting Ice.
 */
public sealed class BasicStComboAnalyzer : Analyzer
{
    private const int WintersEmbraceDurationMs = 3000;
    private const double WintersEmbraceIncrease = 0.20;
    private const int BaselineGlobalCooldownMs = 1500;
    private const int BaselineGlacialBlastCastTimeMs = 1500;

    private long _totalBonusDamage;
    private int _buffedDamageEventCount;
    private readonly Dictionary<int, (string Name, long Damage)> _bonusDamageBySpell = [];

    /// <summary>Total effective damage attributable to the Winter's Embrace 20% buff.</summary>
    public long TotalBonusDamage => _totalBonusDamage;
    public int BuffedDamageEventCount => _buffedDamageEventCount;
    public IReadOnlyDictionary<int, (string Name, long Damage)> BonusDamageBySpell => _bonusDamageBySpell;

    public AnalyzerScoreCard ScoreCard { get; private set; } = null!;
    public int EvaluatedWindows { get; private set; }
    public int SuccessfulWindows { get; private set; }
    public int PartialWindows { get; private set; }
    public int IgnoredAoeWindows { get; private set; }
    public List<StComboWindowEvaluation> Windows { get; private set; } = [];
    public IReadOnlyList<RimeAnalyzerFinding> Findings { get; private set; } = [];

    private StComboWindowEvaluation? _currentWindow;

    public override void Initialize()
    {
        AddEventListener(Events.ApplyBuff.By(SELECTED_PLAYER).Spell(Spells.WintersEmbrace), OnWintersEmbraceApplied);
        AddEventListener(Events.RemoveBuff.By(SELECTED_PLAYER).Spell(Spells.WintersEmbrace), OnWintersEmbraceRemoved);
        AddEventListener(Events.Damage.By(SELECTED_PLAYER), OnDamage);
        AddEventListener(Events.Damage.By(SELECTED_PLAYER).Spell(Spells.BurstingIceDamage), OnBurstingIceDamage);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
    }

    private void OnWintersEmbraceApplied(ApplyBuffEvent @event)
    {
        _currentWindow = new StComboWindowEvaluation()
        {
            StartTimestamp = @event.Timestamp,
            EndTimestamp = @event.Timestamp + WintersEmbraceDurationMs,
        };
    }

    private void OnWintersEmbraceRemoved(RemoveBuffEvent @event)
    {
        _currentWindow!.EndTimestamp = @event.Timestamp;
        Windows.Add(_currentWindow!);
        _currentWindow = null;
    }

    private void OnCast(CastEvent castEvent)
    {
        if (_currentWindow is not null && 
            castEvent.Ability.Id != Spells.BurstingIce.Guid &&
            castEvent.GlobalCooldown is not null)
        {
            _currentWindow.CastsInWindow.Add(castEvent);
        }
    }

    private void OnDamage(DamageEvent damageEvent)
    {
        if (_currentWindow is null)
            return;

        // Winter's Embrace does not affect Bursting Ice itself
        if (damageEvent.Ability.Id == Spells.BurstingIce.Guid ||
            damageEvent.Ability.Id == Spells.BurstingIceDamage.Guid)
            return;

        var bonus = CombatMath.CalculateEffectiveDamage(damageEvent, WintersEmbraceIncrease);
        _totalBonusDamage += bonus;
        _buffedDamageEventCount++;

        var id = damageEvent.Ability.Id;
        var name = damageEvent.Ability.Name;
        _bonusDamageBySpell[id] = _bonusDamageBySpell.TryGetValue(id, out var existing)
            ? (existing.Name, existing.Damage + bonus)
            : (name, bonus);
    }

    private void OnBurstingIceDamage(DamageEvent damageEvent)
    {
        if (_currentWindow is null)
            return;

        _currentWindow.UniqueBurstingIceTargets.Add(damageEvent.TargetId);
    }


    public override void Complete()
    {
        var evaluations = new List<StComboWindowEvaluation>();
        var findings = new List<RimeAnalyzerFinding>();
        foreach (var window in Windows)
        {
            evaluations.Add(EvaluateWindow(window));
        }

        var ignoredAoeWindows = evaluations.Count(w => w.WindowType == BurstingIceWindowType.Aoe);

        var evaluatedWindows = evaluations.Count;
        var successfulWindows = evaluations.Count(w => w.Successful);
        var partialWindows = evaluations.Count(w => w.Partial);
        var score = evaluatedWindows == 0
            ? 0
            : (int)Math.Round(((successfulWindows + (partialWindows * 0.5)) / evaluatedWindows) * 100);

        if (evaluatedWindows == 0)
        {
            findings.Add(new RimeAnalyzerFinding("info", "No Bursting Ice windows were detected in the sample."));
        }
        else
        {
            findings.Add(new RimeAnalyzerFinding("info",
                $"{successfulWindows} of {evaluatedWindows} evaluated Bursting Ice windows matched the expected usage pattern."));

            foreach (var failedWindow in evaluations.Where(w => !w.Successful).Take(5))
            {
                findings.Add(new RimeAnalyzerFinding(
                    failedWindow.Partial ? "warning" : "major",
                    failedWindow.Outcome,
                    failedWindow.StartTimestamp));
            }
        }

        var summary = evaluatedWindows == 0
            ? "No Bursting Ice windows detected in the sample."
            : $"{successfulWindows}/{evaluatedWindows} Bursting Ice windows matched the expected ST or AoE follow-up.";

        ScoreCard = new AnalyzerScoreCard("Bursting Ice Usage", score, summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");
        EvaluatedWindows = evaluatedWindows;
        SuccessfulWindows = successfulWindows;
        PartialWindows = partialWindows;
        IgnoredAoeWindows = ignoredAoeWindows;
        Windows = evaluations;
        Findings = findings;
    }

    private StComboWindowEvaluation EvaluateWindow(StComboWindowEvaluation window)
    {
        var castsInWindow = GetWindowCasts(window);
        var relevantCasts = GetRelevantCasts(castsInWindow);
        var windowType = ClassifyWindow(window);

        return windowType == BurstingIceWindowType.Aoe
            ? EvaluateAoeWindow(window, castsInWindow, relevantCasts)
            : EvaluateSingleTargetWindow(window, castsInWindow, relevantCasts);
    }

    private static List<CastEvent> GetWindowCasts(StComboWindowEvaluation window) =>
        [.. window.CastsInWindow.Where(c => c.Timestamp > window.StartTimestamp && c.Timestamp <= window.EndTimestamp)];

    private static List<CastEvent> GetRelevantCasts(IEnumerable<CastEvent> casts) =>
        [.. casts.Where(c =>
                c.Ability.Id == Spells.GlacialBlast.Id ||
                c.Ability.Id == Spells.ColdSnap.Id ||
                c.Ability.Id == Spells.FreezingTorrent.Id ||
                c.Ability.Id == Spells.IceComet.Id)];

    private static BurstingIceWindowType ClassifyWindow(StComboWindowEvaluation window) =>
        window.UniqueBurstingIceTargets.Count >= 2 ? BurstingIceWindowType.Aoe : BurstingIceWindowType.SingleTarget;

    private static bool IsGlacialBlast(CastEvent cast) => cast.Ability.Id == Spells.GlacialBlast.Id;
    private static bool IsIceComet(CastEvent cast) => cast.Ability.Id == Spells.IceComet.Id;
    private static bool IsColdSnap(CastEvent cast) => cast.Ability.Id == Spells.ColdSnap.Id;
    private static bool IsFreezingTorrent(CastEvent cast) => cast.Ability.Id == Spells.FreezingTorrent.Id;
    private static bool IsFinisher(CastEvent cast) => IsColdSnap(cast) || IsFreezingTorrent(cast);

    private StComboWindowEvaluation EvaluateSingleTargetWindow(
        StComboWindowEvaluation window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var glacialBlasts = relevantCasts.Count(IsGlacialBlast);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var firstRelevantCast = relevantCasts.FirstOrDefault();
        var timeBudget = Math.Max(window.EndTimestamp - window.StartTimestamp, 0);
        var expectedGlacialBlasts = GetExpectedSingleTargetGlacialBlasts(timeBudget);
        var finisherExpected = ShouldExpectSingleTargetFinisher(timeBudget, expectedGlacialBlasts);
        var missingGlacialBlasts = Math.Max(expectedGlacialBlasts - glacialBlasts, 0);
        var hasValidFinisher = coldSnaps > 0 || freezingTorrents > 0;
        var usedFreezingTorrentCombo = freezingTorrents > 0 && coldSnaps > 0;
        var openedWithGlacialBlast = firstRelevantCast is not null && IsGlacialBlast(firstRelevantCast);

        window.WindowType = BurstingIceWindowType.SingleTarget;
        window.TargetCount = window.UniqueBurstingIceTargets.Count;
        window.CastsInWindow = castsInWindow;
        window.GlacialBlastCount = glacialBlasts;
        window.IceCometCount = relevantCasts.Count(IsIceComet);
        window.ColdSnapCount = coldSnaps;
        window.FreezingTorrentCount = freezingTorrents;
        window.ExpectedGlacialBlastCount = expectedGlacialBlasts;
        window.ExpectsFinisher = finisherExpected;

        window.Successful = usedFreezingTorrentCombo || (openedWithGlacialBlast && missingGlacialBlasts == 0 && (!finisherExpected || hasValidFinisher));
        window.Partial = !window.Successful && (
            (glacialBlasts > 0 && !finisherExpected) ||
            (glacialBlasts > 0 && hasValidFinisher) ||
            (freezingTorrents > 0 && coldSnaps > 0));
        window.Outcome = BuildSingleTargetOutcome(window, firstRelevantCast, missingGlacialBlasts, hasValidFinisher, finisherExpected, usedFreezingTorrentCombo);

        return window;
    }

    private StComboWindowEvaluation EvaluateAoeWindow(
        StComboWindowEvaluation window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var iceComets = relevantCasts.Count(IsIceComet);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);

        window.WindowType = BurstingIceWindowType.Aoe;
        window.TargetCount = window.UniqueBurstingIceTargets.Count;
        window.CastsInWindow = castsInWindow;
        window.GlacialBlastCount = relevantCasts.Count(IsGlacialBlast);
        window.IceCometCount = iceComets;
        window.ColdSnapCount = coldSnaps;
        window.FreezingTorrentCount = freezingTorrents;
        window.ExpectedGlacialBlastCount = 0;
        window.ExpectsFinisher = true;

        var hasRequiredIceComets = iceComets >= 2;
        var hasRequiredColdSnap = coldSnaps >= 1;

        window.Successful = hasRequiredIceComets && hasRequiredColdSnap;
        window.Partial = !window.Successful && (iceComets > 0 || coldSnaps > 0 || freezingTorrents > 0);
        window.Outcome = BuildAoeOutcome(window, hasRequiredIceComets, hasRequiredColdSnap);

        return window;
    }

    private static int GetExpectedSingleTargetGlacialBlasts(int timeBudgetMs)
    {
        if (timeBudgetMs >= (BaselineGlacialBlastCastTimeMs * 2) + BaselineGlobalCooldownMs)
            return 2;

        return timeBudgetMs >= BaselineGlacialBlastCastTimeMs ? 1 : 0;
    }

    private static bool ShouldExpectSingleTargetFinisher(int timeBudgetMs, int expectedGlacialBlasts)
    {
        if (expectedGlacialBlasts <= 0)
            return false;

        var requiredTime = (expectedGlacialBlasts * BaselineGlacialBlastCastTimeMs) + BaselineGlobalCooldownMs;
        return timeBudgetMs >= requiredTime;
    }

    private static string BuildSingleTargetOutcome(
        StComboWindowEvaluation window,
        CastEvent? firstRelevantCast,
        int missingGlacialBlasts,
        bool hasValidFinisher,
        bool finisherExpected,
        bool usedFreezingTorrentCombo)
    {
        if (window.Successful)
        {
            if (usedFreezingTorrentCombo)
            {
                return "Used the Winter's Embrace window well for single-target damage with the Freezing Torrent plus Cold Snap variant.";
            }

            return finisherExpected
                ? "Used the Winter's Embrace window well for single-target damage, fitting the expected Glacial Blasts and a finisher."
                : "Used the Winter's Embrace window well for single-target damage, fitting the expected Glacial Blast usage.";
        }

        var reasons = new List<string>();

        if (firstRelevantCast is null)
        {
            reasons.Add("No single-target follow-up spell was cast during Winter's Embrace");
        }
        else if (!(IsGlacialBlast(firstRelevantCast) || IsFreezingTorrent(firstRelevantCast)))
        {
            reasons.Add($"Opened with {firstRelevantCast.Ability.Name} instead of starting with Glacial Blast or Freezing Torrent");
        }

        if (missingGlacialBlasts > 0)
        {
            reasons.Add(missingGlacialBlasts == 1
                ? "one expected Glacial Blast was missed"
                : $"{missingGlacialBlasts} expected Glacial Blasts were missed");
        }

        if (finisherExpected && !hasValidFinisher)
        {
            reasons.Add("there was enough time for a finisher, but neither Cold Snap nor Freezing Torrent was used to finish the window");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("The window only partially matched the expected single-target follow-up");
        }

        return JoinReasons(reasons);
    }

    private static string BuildAoeOutcome(StComboWindowEvaluation window, bool hasRequiredIceComets, bool hasRequiredColdSnap)
    {
        if (window.Successful)
        {
            return "Used the AoE Winter's Embrace window well, fitting two Ice Comets and one Cold Snap in any order.";
        }

        var reasons = new List<string>();

        if (!hasRequiredIceComets)
        {
            reasons.Add(window.IceCometCount == 0
                ? "no Ice Comets were used"
                : "the window did not include the expected second Ice Comet");
        }

        if (!hasRequiredColdSnap)
        {
            reasons.Add("Cold Snap was missing from the AoE window");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("The AoE window only partially matched the expected follow-up");
        }

        return JoinReasons(reasons);
    }

    private static string JoinReasons(List<string> reasons)
    {
        if (reasons.Count == 1)
            return reasons[0] + ".";

        var builder = new StringBuilder();
        for (var i = 0; i < reasons.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == reasons.Count - 1 ? ", and " : ", ");
            }

            builder.Append(reasons[i]);
        }

        builder.Append('.');
        return builder.ToString();
    }

    public sealed class StComboWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int EndTimestamp { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public BurstingIceWindowType WindowType { get; set; }
        public int TargetCount { get; set; }
        public bool Successful { get; set; }
        public bool Partial { get; set; }
        public int GlacialBlastCount { get; set; }
        public int IceCometCount { get; set; }
        public int ColdSnapCount { get; set; }
        public int FreezingTorrentCount { get; set; }
        public int ExpectedGlacialBlastCount { get; set; }
        public bool ExpectsFinisher { get; set; }
        public List<CastEvent> CastsInWindow { get; set; } = [];
        public HashSet<int> UniqueBurstingIceTargets { get; } = [];
    }

    public enum BurstingIceWindowType
    {
        SingleTarget,
        Aoe,
    }
}
