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

/// <summary>
/// Shared machinery for evaluating Winter's Embrace windows: captures each window's casts and
/// Bursting Ice targets, detects the Winter Orb spender loadout, and accounts the 20% damage
/// amplifier. It is the surface type for the pull read paths; <see cref="SingleTargetRimeCombo"/>
/// and <see cref="AoERimeCombo"/> each score the captured windows against their pull shape's
/// expected follow-up via <see cref="EvaluateWindow"/>.
/// </summary>
public abstract partial class BasicStComboAnalyzer : Analyzer
{
    private const int WintersEmbraceDurationMs = 3000;
    private const double WintersEmbraceIncrease = 0.20;

    private readonly Dictionary<int, (string Name, long Damage)> _bonusDamageBySpell = [];
    private readonly List<WindowCapture> _windows = [];
    private readonly List<StComboWindowEvaluation> _evaluations = [];
    private readonly Dictionary<int, int> _spenderCastCounts = [];

    private WindowCapture? _currentWindow;

    private int _stSpenderId;
    private int _aoeSpenderId;

    private bool _materialized;
    private RimeBuild _build;

    /// <summary>The Winter Orb spender loadout detected for this pull.</summary>
    public RimeBuild Build { get { EnsureMaterialized(); return _build; } }

    /// <summary>Every Winter's Embrace window evaluated for this pull, in encounter order.</summary>
    public IReadOnlyList<StComboWindowEvaluation> Windows { get { EnsureMaterialized(); return _evaluations; } }

    /// <summary>Total Winter's Embrace windows evaluated for this pull.</summary>
    public int EvaluatedWindows { get { EnsureMaterialized(); return _evaluations.Count; } }

    /// <summary>Windows that fully matched the expected follow-up for this pull's shape.</summary>
    public int SuccessfulWindows { get { EnsureMaterialized(); return _evaluations.Count(w => w.Successful); } }

    /// <summary>Windows that only partially matched the expected follow-up.</summary>
    public int PartialWindows { get { EnsureMaterialized(); return _evaluations.Count(w => w.Partial); } }

    /// <summary>Total extra damage attributable to the Winter's Embrace 20% amplifier.</summary>
    public long TotalBonusDamage { get; private set; }

    /// <summary>Number of player damage events amplified by Winter's Embrace.</summary>
    public int BuffedDamageEventCount { get; private set; }

    /// <summary>The detected build's single-target Winter Orb spender name.</summary>
    protected string StSpenderName { get; private set; } = "Glacial Blast";

    /// <summary>The detected build's AoE Winter Orb spender name.</summary>
    protected string AoeSpenderName { get; private set; } = "Ice Comet";

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnWintersEmbraceApplied(ApplyBuffEvent @event)
    {
        _currentWindow = new WindowCapture
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

    /// <summary>Detects the build and scores every captured window via <see cref="EvaluateWindow"/>, once on first read.</summary>
    private void EnsureMaterialized()
    {
        if (_materialized) return;
        _materialized = true;

        _build = DetectBuild();
        _stSpenderId = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Id : Spells.GlacialBlast.Id;
        _aoeSpenderId = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Id : Spells.IceComet.Id;
        StSpenderName = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Name : Spells.GlacialBlast.Name;
        AoeSpenderName = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Name : Spells.IceComet.Name;

        foreach (var window in _windows)
            _evaluations.Add(EvaluateWindow(window));
    }

    /// <summary>Scores one captured Winter's Embrace window against the follow-up this analyzer's pull shape expects.</summary>
    protected abstract StComboWindowEvaluation EvaluateWindow(WindowCapture window);

    /// <summary>The GCD casts that landed strictly inside the window.</summary>
    protected static List<CastEvent> GetWindowCasts(WindowCapture window) =>
        [.. window.CastsInWindow.Where(c => c.Timestamp > window.StartTimestamp && c.Timestamp <= window.EndTimestamp)];

    /// <summary>Filters casts down to the detected build's spenders and the finisher spells.</summary>
    protected List<CastEvent> GetRelevantCasts(IEnumerable<CastEvent> casts) =>
        [.. casts.Where(c =>
                c.Ability.Id == _stSpenderId ||
                c.Ability.Id == _aoeSpenderId ||
                c.Ability.Id == Spells.ColdSnap.Id ||
                c.Ability.Id == Spells.FreezingTorrent.Id)];

    protected bool IsStSpender(CastEvent cast) => cast.Ability.Id == _stSpenderId;
    protected bool IsAoeSpender(CastEvent cast) => cast.Ability.Id == _aoeSpenderId;
    protected static bool IsColdSnap(CastEvent cast) => cast.Ability.Id == Spells.ColdSnap.Id;
    protected static bool IsFreezingTorrent(CastEvent cast) => cast.Ability.Id == Spells.FreezingTorrent.Id;

    /// <summary>One Winter's Embrace window as captured during dispatch, before scoring.</summary>
    public sealed class WindowCapture
    {
        public int StartTimestamp { get; init; }
        public int EndTimestamp { get; set; }
        public List<CastEvent> CastsInWindow { get; } = [];
        public HashSet<int> UniqueBurstingIceTargets { get; } = [];
    }

    /// <summary>
    /// The scored outcome of one Winter's Embrace window: the members every window shares
    /// regardless of pull shape. Each concrete analyzer returns its own subtype carrying the
    /// shape-specific detail.
    /// </summary>
    public abstract class StComboWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int EndTimestamp { get; set; }
        public RimeBuild Build { get; set; }
        public string StSpenderName { get; set; } = "Glacial Blast";
        public string AoeSpenderName { get; set; } = "Ice Comet";

        /// <summary>Unique targets hit by Bursting Ice damage during the window.</summary>
        public int TargetCount { get; set; }

        public bool Successful { get; set; }
        public bool Partial { get; set; }
        public int StSpenderCount { get; set; }
        public int AoeSpenderCount { get; set; }
        public int ColdSnapCount { get; set; }
        public int FreezingTorrentCount { get; set; }
        public List<CastEvent> CastsInWindow { get; set; } = [];
    }
}
