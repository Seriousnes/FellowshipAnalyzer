using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/**
 * While Bursting Ice is active on an enemy you gain Winter's Embrace, causing you to deal 20% more damage.
 *
 * Winter's Embrace does not affect Bursting Ice.
 */
public abstract partial class BasicStComboAnalyzer : Analyzer
{
    private const int WintersEmbraceDurationMs = 3000;
    private const double WintersEmbraceIncrease = 0.20;
    private const int BaselineGlobalCooldownMs = 1500;
    private const int BaselineSpenderCastTimeMs = 1500;

    private readonly Dictionary<int, (string Name, long Damage)> _bonusDamageBySpell = [];
    private readonly List<StComboWindowEvaluation> _windows = [];
    private readonly List<StComboWindowEvaluation> _evaluations = [];
    private readonly Dictionary<int, int> _spenderCastCounts = [];

    private StComboWindowEvaluation? _currentWindow;

    private int _stSpenderId;
    private int _aoeSpenderId;
    private string _stSpenderName = "Glacial Blast";
    private string _aoeSpenderName = "Ice Comet";

    /// <summary>The Winter Orb spender loadout detected for this pull.</summary>
    public RimeBuild Build { get; private set; }

    /// <summary>Every Winter's Embrace window evaluated for this pull, in encounter order.</summary>
    public IReadOnlyList<StComboWindowEvaluation> Windows => _evaluations;

    /// <summary>Total Winter's Embrace windows evaluated for this pull.</summary>
    public int EvaluatedWindows => _evaluations.Count;

    /// <summary>Windows that fully matched the expected follow-up for this pull's shape.</summary>
    public int SuccessfulWindows { get; private set; }

    /// <summary>Windows that only partially matched the expected follow-up.</summary>
    public int PartialWindows { get; private set; }

    /// <summary>Windows classified as AoE (Bursting Ice hit 2 or more unique targets).</summary>
    public int IgnoredAoeWindows { get; private set; }

    /// <summary>Total extra damage attributable to the Winter's Embrace 20% amplifier.</summary>
    public long TotalBonusDamage { get; private set; }

    /// <summary>Number of player damage events amplified by Winter's Embrace.</summary>
    public int BuffedDamageEventCount { get; private set; }

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
        TotalBonusDamage += bonus;
        BuffedDamageEventCount++;

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

    public override void OnPullEnd()
    {
        Build = DetectBuild();
        _stSpenderId = Build == RimeBuild.IcyTalons ? Spells.TalonStrike.Id : Spells.GlacialBlast.Id;
        _aoeSpenderId = Build == RimeBuild.IcyTalons ? Spells.RisingTalons.Id : Spells.IceComet.Id;
        _stSpenderName = Build == RimeBuild.IcyTalons ? Spells.TalonStrike.Name : Spells.GlacialBlast.Name;
        _aoeSpenderName = Build == RimeBuild.IcyTalons ? Spells.RisingTalons.Name : Spells.IceComet.Name;

        foreach (var window in _windows)
            _evaluations.Add(EvaluateWindow(window));

        SuccessfulWindows = _evaluations.Count(w => w.Successful);
        PartialWindows = _evaluations.Count(w => w.Partial);
        IgnoredAoeWindows = _evaluations.Count(w => w.WindowType == BurstingIceWindowType.Aoe);
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
            Build = Build,
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
            FirstRelevantCastName = firstRelevantCast?.Ability.Name,
            OpenedWithExpectedCast = firstRelevantCast is not null &&
                (IsStSpender(firstRelevantCast) || IsFreezingTorrent(firstRelevantCast)),
            MissingStSpenderCount = missingStSpenders,
            HasValidFinisher = hasValidFinisher,
            UsedFreezingTorrentCombo = usedFreezingTorrentCombo,
        };
        foreach (var target in window.UniqueBurstingIceTargets)
            result.UniqueBurstingIceTargets.Add(target);

        result.Successful = usedFreezingTorrentCombo || (openedWithStSpender && missingStSpenders == 0 && (!finisherExpected || hasValidFinisher));
        result.Partial = !result.Successful && (
            (stSpenders > 0 && !finisherExpected) ||
            (stSpenders > 0 && hasValidFinisher) ||
            (freezingTorrents > 0 && coldSnaps > 0));

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
        var hasRequiredAoeSpenders = aoeSpenders >= 2;
        var hasRequiredColdSnap = coldSnaps >= 1;

        var result = new StComboWindowEvaluation
        {
            StartTimestamp = window.StartTimestamp,
            EndTimestamp = window.EndTimestamp,
            WindowType = BurstingIceWindowType.Aoe,
            Build = Build,
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
            HasRequiredAoeSpenders = hasRequiredAoeSpenders,
            HasRequiredColdSnap = hasRequiredColdSnap,
        };
        foreach (var target in window.UniqueBurstingIceTargets)
            result.UniqueBurstingIceTargets.Add(target);

        result.Successful = hasRequiredAoeSpenders && hasRequiredColdSnap;
        result.Partial = !result.Successful && (aoeSpenders > 0 || coldSnaps > 0 || freezingTorrents > 0);

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

    public sealed class StComboWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int EndTimestamp { get; set; }
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

        /// <summary>Whether an AoE window contained the expected two AoE spender casts.</summary>
        public bool HasRequiredAoeSpenders { get; set; }

        /// <summary>Whether an AoE window contained the expected Cold Snap.</summary>
        public bool HasRequiredColdSnap { get; set; }

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
