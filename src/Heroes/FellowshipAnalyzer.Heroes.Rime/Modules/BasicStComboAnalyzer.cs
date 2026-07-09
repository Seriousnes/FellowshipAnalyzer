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
public abstract partial class BasicStComboAnalyzer : Analyzer<BasicStComboReport>
{
    private const int WintersEmbraceDurationMs = 3000;
    private const double WintersEmbraceIncrease = 0.20;
    private const int BaselineGlobalCooldownMs = 1500;
    private const int BaselineSpenderCastTimeMs = 1500;

    private long _totalBonusDamage;
    private int _buffedDamageEventCount;
    private readonly Dictionary<int, (string Name, long Damage)> _bonusDamageBySpell = [];
    private readonly List<StComboWindowEvaluation> _windows = [];
    private readonly Dictionary<int, int> _spenderCastCounts = [];

    private StComboWindowEvaluation? _currentWindow;

    private RimeBuild _build;
    private int _stSpenderId;
    private int _aoeSpenderId;
    private string _stSpenderName = "Glacial Blast";
    private string _aoeSpenderName = "Ice Comet";

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnWintersEmbraceApplied(ApplyBuffEvent @event)
    {
        _currentWindow = new StComboWindowEvaluation()
        {
            StartTimestamp = @event.Timestamp,
            EndTimestamp = @event.Timestamp + WintersEmbraceDurationMs,
        };
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnWintersEmbraceRemoved(RemoveBuffEvent @event)
    {
        _currentWindow!.EndTimestamp = @event.Timestamp;
        _windows.Add(_currentWindow!);
        _currentWindow = null;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        TallySpender(castEvent.Ability.Id);

        if (_currentWindow is not null &&
            castEvent.Ability.Id != Spells.BurstingIce.FSLID &&
            castEvent.GlobalCooldown is not null)
        {
            _currentWindow.CastsInWindow.Add(castEvent);
        }
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        if (_currentWindow is null)
            return;

        if (damageEvent.Ability.Id == Spells.BurstingIce.FSLID ||
            damageEvent.Ability.Id == Spells.BurstingIceDamage.FSLID)
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

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.BurstingIceDamage))]
    private void OnBurstingIceDamage(DamageEvent damageEvent)
    {
        if (_currentWindow is null)
            return;

        _currentWindow.UniqueBurstingIceTargets.Add(damageEvent.TargetId);
    }

    private void TallySpender(int abilityId)
    {
        if (abilityId == Spells.GlacialBlast.Id ||
            abilityId == Spells.IceComet.Id ||
            abilityId == Spells.TalonStrike.Id ||
            abilityId == Spells.RisingTalons.Id)
        {
            _spenderCastCounts[abilityId] = _spenderCastCounts.GetValueOrDefault(abilityId) + 1;
        }
    }

    /// <summary>
    /// Detects which Winter Orb spender loadout the player is running by comparing how often they
    /// cast the Icy Talons spenders against the default spenders across this pull.
    /// </summary>
    private RimeBuild DetectBuild()
    {
        var talons = _spenderCastCounts.GetValueOrDefault(Spells.TalonStrike.Id)
            + _spenderCastCounts.GetValueOrDefault(Spells.RisingTalons.Id);
        var frostfire = _spenderCastCounts.GetValueOrDefault(Spells.GlacialBlast.Id)
            + _spenderCastCounts.GetValueOrDefault(Spells.IceComet.Id);

        return talons > frostfire ? RimeBuild.IcyTalons : RimeBuild.Default;
    }

    /// <summary>Per-pull projection of this analyzer's accumulated state for the closing pull.</summary>
    public override BasicStComboReport OnPullEnd()
    {
        _build = DetectBuild();
        _stSpenderId = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Id : Spells.GlacialBlast.Id;
        _aoeSpenderId = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Id : Spells.IceComet.Id;
        _stSpenderName = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Name : Spells.GlacialBlast.Name;
        _aoeSpenderName = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Name : Spells.IceComet.Name;

        var evaluations = new List<StComboWindowEvaluation>(_windows.Count);
        var findings = new List<RimeAnalyzerFinding>();
        foreach (var window in _windows)
            evaluations.Add(EvaluateWindow(window));

        var ignoredAoeWindows = evaluations.Count(w => w.WindowType == BurstingIceWindowType.Aoe);
        var evaluatedWindows = evaluations.Count;
        var successfulWindows = evaluations.Count(w => w.Successful);
        var partialWindows = evaluations.Count(w => w.Partial);
        var score = evaluatedWindows == 0
            ? 0
            : (int)Math.Round(((successfulWindows + (partialWindows * 0.5)) / evaluatedWindows) * 100);

        var buildLabel = _build == RimeBuild.IcyTalons ? "Icy Talons" : "default";

        if (evaluatedWindows == 0)
        {
            findings.Add(new RimeAnalyzerFinding("info", "No Bursting Ice windows were detected in the sample."));
        }
        else
        {
            findings.Add(new RimeAnalyzerFinding("info",
                $"Scored against the {buildLabel} build ({_stSpenderName} / {_aoeSpenderName} spenders). "
                + $"{successfulWindows} of {evaluatedWindows} evaluated Bursting Ice windows matched the expected usage pattern."));

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

        var scoreCard = new AnalyzerScoreCard("Bursting Ice Usage", score, summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        return new BasicStComboReport(
            scoreCard,
            _build,
            evaluatedWindows,
            successfulWindows,
            partialWindows,
            ignoredAoeWindows,
            _totalBonusDamage,
            _buffedDamageEventCount,
            evaluations,
            findings);
    }

    /// <summary>Evaluates one Winter's Embrace window against the follow-up this analyzer's pull shape expects.</summary>
    protected abstract StComboWindowEvaluation EvaluateWindow(StComboWindowEvaluation window);

    protected static List<CastEvent> GetWindowCasts(StComboWindowEvaluation window) =>
        [.. window.CastsInWindow.Where(c => c.Timestamp > window.StartTimestamp && c.Timestamp <= window.EndTimestamp)];

    protected List<CastEvent> GetRelevantCasts(IEnumerable<CastEvent> casts) =>
        [.. casts.Where(c =>
                c.Ability.Id == _stSpenderId ||
                c.Ability.Id == _aoeSpenderId ||
                c.Ability.Id == Spells.ColdSnap.Id ||
                c.Ability.Id == Spells.FreezingTorrent.Id)];

    private bool IsStSpender(CastEvent cast) => cast.Ability.Id == _stSpenderId;
    private bool IsAoeSpender(CastEvent cast) => cast.Ability.Id == _aoeSpenderId;
    private static bool IsColdSnap(CastEvent cast) => cast.Ability.Id == Spells.ColdSnap.Id;
    private static bool IsFreezingTorrent(CastEvent cast) => cast.Ability.Id == Spells.FreezingTorrent.Id;

    protected StComboWindowEvaluation EvaluateSingleTargetWindow(
        StComboWindowEvaluation window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var stSpenders = relevantCasts.Count(IsStSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);
        var firstRelevantCast = relevantCasts.FirstOrDefault();
        var timeBudget = Math.Max(window.EndTimestamp - window.StartTimestamp, 0);
        var expectedStSpenders = GetExpectedSingleTargetSpenders(timeBudget);
        var finisherExpected = ShouldExpectSingleTargetFinisher(timeBudget, expectedStSpenders);
        var missingStSpenders = Math.Max(expectedStSpenders - stSpenders, 0);
        var hasValidFinisher = coldSnaps > 0 || freezingTorrents > 0;
        var usedFreezingTorrentCombo = freezingTorrents > 0 && coldSnaps > 0;
        var openedWithStSpender = firstRelevantCast is not null && IsStSpender(firstRelevantCast);

        var result = new StComboWindowEvaluation
        {
            StartTimestamp = window.StartTimestamp,
            EndTimestamp = window.EndTimestamp,
            WindowType = BurstingIceWindowType.SingleTarget,
            Build = _build,
            StSpenderName = _stSpenderName,
            AoeSpenderName = _aoeSpenderName,
            TargetCount = window.UniqueBurstingIceTargets.Count,
            CastsInWindow = castsInWindow,
            StSpenderCount = stSpenders,
            AoeSpenderCount = relevantCasts.Count(IsAoeSpender),
            ColdSnapCount = coldSnaps,
            FreezingTorrentCount = freezingTorrents,
            ExpectedStSpenderCount = expectedStSpenders,
            ExpectsFinisher = finisherExpected,
        };
        foreach (var target in window.UniqueBurstingIceTargets)
            result.UniqueBurstingIceTargets.Add(target);

        result.Successful = usedFreezingTorrentCombo || (openedWithStSpender && missingStSpenders == 0 && (!finisherExpected || hasValidFinisher));
        result.Partial = !result.Successful && (
            (stSpenders > 0 && !finisherExpected) ||
            (stSpenders > 0 && hasValidFinisher) ||
            (freezingTorrents > 0 && coldSnaps > 0));
        result.Outcome = BuildSingleTargetOutcome(result, firstRelevantCast, missingStSpenders, hasValidFinisher, finisherExpected, usedFreezingTorrentCombo);

        return result;
    }

    protected StComboWindowEvaluation EvaluateAoeWindow(
        StComboWindowEvaluation window,
        List<CastEvent> castsInWindow,
        List<CastEvent> relevantCasts)
    {
        var aoeSpenders = relevantCasts.Count(IsAoeSpender);
        var coldSnaps = relevantCasts.Count(IsColdSnap);
        var freezingTorrents = relevantCasts.Count(IsFreezingTorrent);

        var result = new StComboWindowEvaluation
        {
            StartTimestamp = window.StartTimestamp,
            EndTimestamp = window.EndTimestamp,
            WindowType = BurstingIceWindowType.Aoe,
            Build = _build,
            StSpenderName = _stSpenderName,
            AoeSpenderName = _aoeSpenderName,
            TargetCount = window.UniqueBurstingIceTargets.Count,
            CastsInWindow = castsInWindow,
            StSpenderCount = relevantCasts.Count(IsStSpender),
            AoeSpenderCount = aoeSpenders,
            ColdSnapCount = coldSnaps,
            FreezingTorrentCount = freezingTorrents,
            ExpectedStSpenderCount = 0,
            ExpectsFinisher = true,
        };
        foreach (var target in window.UniqueBurstingIceTargets)
            result.UniqueBurstingIceTargets.Add(target);

        var hasRequiredAoeSpenders = aoeSpenders >= 2;
        var hasRequiredColdSnap = coldSnaps >= 1;

        result.Successful = hasRequiredAoeSpenders && hasRequiredColdSnap;
        result.Partial = !result.Successful && (aoeSpenders > 0 || coldSnaps > 0 || freezingTorrents > 0);
        result.Outcome = BuildAoeOutcome(result, hasRequiredAoeSpenders, hasRequiredColdSnap);

        return result;
    }

    private static int GetExpectedSingleTargetSpenders(int timeBudgetMs)
    {
        if (timeBudgetMs >= (BaselineSpenderCastTimeMs * 2) + BaselineGlobalCooldownMs)
            return 2;

        return timeBudgetMs >= BaselineSpenderCastTimeMs ? 1 : 0;
    }

    private static bool ShouldExpectSingleTargetFinisher(int timeBudgetMs, int expectedSpenders)
    {
        if (expectedSpenders <= 0)
            return false;

        var requiredTime = (expectedSpenders * BaselineSpenderCastTimeMs) + BaselineGlobalCooldownMs;
        return timeBudgetMs >= requiredTime;
    }

    private string BuildSingleTargetOutcome(
        StComboWindowEvaluation window,
        CastEvent? firstRelevantCast,
        int missingStSpenders,
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
                ? $"Used the Winter's Embrace window well for single-target damage, fitting the expected {_stSpenderName} casts and a finisher."
                : $"Used the Winter's Embrace window well for single-target damage, fitting the expected {_stSpenderName} usage.";
        }

        var reasons = new List<string>();

        if (firstRelevantCast is null)
        {
            reasons.Add("No single-target follow-up spell was cast during Winter's Embrace");
        }
        else if (!(IsStSpender(firstRelevantCast) || IsFreezingTorrent(firstRelevantCast)))
        {
            reasons.Add($"Opened with {firstRelevantCast.Ability.Name} instead of starting with {_stSpenderName} or Freezing Torrent");
        }

        if (missingStSpenders > 0)
        {
            reasons.Add(missingStSpenders == 1
                ? $"one expected {_stSpenderName} was missed"
                : $"{missingStSpenders} expected {_stSpenderName} casts were missed");
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

    private string BuildAoeOutcome(StComboWindowEvaluation window, bool hasRequiredAoeSpenders, bool hasRequiredColdSnap)
    {
        if (window.Successful)
        {
            return $"Used the AoE Winter's Embrace window well, fitting two {_aoeSpenderName} casts and one Cold Snap in any order.";
        }

        var reasons = new List<string>();

        if (!hasRequiredAoeSpenders)
        {
            reasons.Add(window.AoeSpenderCount == 0
                ? $"no {_aoeSpenderName} casts were used"
                : $"the window did not include the expected second {_aoeSpenderName}");
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
        public RimeBuild Build { get; set; }
        public string StSpenderName { get; set; } = "Glacial Blast";
        public string AoeSpenderName { get; set; } = "Ice Comet";
        public int TargetCount { get; set; }
        public bool Successful { get; set; }
        public bool Partial { get; set; }
        public int StSpenderCount { get; set; }
        public int AoeSpenderCount { get; set; }
        public int ColdSnapCount { get; set; }
        public int FreezingTorrentCount { get; set; }
        public int ExpectedStSpenderCount { get; set; }
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

/// <summary>
/// Winter's Embrace analyzer for single-target pulls: every window is scored against the
/// single-target follow-up (the detected build's spender, plus a Cold Snap or Freezing Torrent finisher).
/// </summary>
[ForPull(PullKind.Single)]
public sealed class SingleTargetRimeCombo : BasicStComboAnalyzer
{
    protected override StComboWindowEvaluation EvaluateWindow(StComboWindowEvaluation window)
    {
        var castsInWindow = GetWindowCasts(window);
        return EvaluateSingleTargetWindow(window, castsInWindow, GetRelevantCasts(castsInWindow));
    }
}

/// <summary>
/// Winter's Embrace analyzer for multi-target pulls: every window is scored against the AoE
/// follow-up (two of the detected build's AoE spenders and a Cold Snap in any order).
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class AoERimeCombo : BasicStComboAnalyzer
{
    protected override StComboWindowEvaluation EvaluateWindow(StComboWindowEvaluation window)
    {
        var castsInWindow = GetWindowCasts(window);
        return EvaluateAoeWindow(window, castsInWindow, GetRelevantCasts(castsInWindow));
    }
}
