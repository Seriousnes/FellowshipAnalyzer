using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using RimeTalents = FellowshipAnalyzer.Core.Common.Spells.RimeTalents;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Shared machinery for evaluating Winter's Embrace windows. Winter's Embrace is baseline: while
/// Bursting Ice stands on an enemy the player deals 20% more damage with everything except Bursting
/// Ice itself, so every window is a burst of banked Winter Orbs that the opening Bursting Ice paid
/// for. This class captures each window and the state it opened with; the shape-specialized
/// subclasses score the follow-up their pull shape expects.
/// </summary>
/// <remarks>
/// A window is anchored on the player's own Winter's Embrace apply/remove pair, never on a fixed
/// duration, because talents extend the Bursting Ice debuff that drives it. Hardening covers the
/// three ways the pair can be incomplete: a remove with no live window is ignored, a re-apply while
/// a window is open closes the open one at the re-apply, and a window still open when the pull ends
/// closes at the earlier of its baseline expiry and the pull end and is marked
/// <see cref="EmbraceWindowEvaluation.BoundaryTruncated"/> so a guide can tell a clipped window from
/// a real one.
/// <para>
/// The opening Bursting Ice cast supplies two facts the window is judged against: the Winter Orbs
/// banked when it went out (<c>null</c> when the log carries no resource snapshot, as season-two
/// logs do not) and the enemy it landed on. That enemy dying mid-window ends Winter's Embrace early,
/// which reads as <see cref="EmbraceWindowEvaluation.EndedByTargetDeath"/> rather than as a
/// follow-up failure. Ice Blitz and Wrath of Winter are sampled from the tracked aura registry at
/// the instant the window opens, so a pull that begins with a major already standing still sees it.
/// </para>
/// </remarks>
public abstract partial class WintersEmbraceWindowAnalyzer : Analyzer
{
    /// <summary>Bursting Ice's baseline debuff duration in milliseconds, used only to close a window still open at pull end.</summary>
    public const int BaseWindowDurationMs = 3000;

    /// <summary>Winter Orbs banked at the opening Bursting Ice cast at or below which the window opens starved.</summary>
    public const int StarvedOrbThreshold = 1;

    private readonly List<WindowCapture> _windows = [];
    private readonly List<EmbraceWindowEvaluation> _evaluations = [];
    private readonly Dictionary<int, int> _spenderCastCounts = [];

    private WindowCapture? _currentWindow;
    private BurstingIceAnchor? _lastBurstingIce;
    private Combatants? _combatants;

    private bool _materialized;
    private RimeBuild _build;
    private int _stSpenderId;
    private int _aoeSpenderId;

    /// <summary>The Winter Orb spender loadout detected for this pull.</summary>
    public RimeBuild Build { get { EnsureMaterialized(); return _build; } }

    /// <summary>Every Winter's Embrace window evaluated for this pull, in encounter order.</summary>
    public IReadOnlyList<EmbraceWindowEvaluation> Windows { get { EnsureMaterialized(); return _evaluations; } }

    /// <summary>Total Winter's Embrace windows evaluated for this pull.</summary>
    public int EvaluatedWindows => Windows.Count;

    /// <summary>Windows that fully matched the follow-up this pull's shape expects.</summary>
    public int SuccessfulWindows => Windows.Count(window => window.Successful);

    /// <summary>Windows that only partially matched the expected follow-up.</summary>
    public int PartialWindows => Windows.Count(window => window.Partial);

    /// <summary>Windows opened on a Bursting Ice cast with at most <see cref="StarvedOrbThreshold"/> Winter Orbs banked.</summary>
    public int StarvedWindows => Windows.Count(window => window.OpenedStarved);

    /// <summary>Windows cut short by the Bursting Ice target dying while the debuff was still ticking.</summary>
    public int WindowsEndedByTargetDeath => Windows.Count(window => window.EndedByTargetDeath);

    /// <summary>Windows whose opening Bursting Ice cast carried a Winter Orb snapshot.</summary>
    public int WindowsWithOrbData => Windows.Count(window => window.OrbsBankedAtOpen is not null);

    /// <summary>The detected build's single-target Winter Orb spender name.</summary>
    public string StSpenderName { get { EnsureMaterialized(); return _stSpenderName; } }

    /// <summary>The detected build's AoE Winter Orb spender name.</summary>
    public string AoeSpenderName { get { EnsureMaterialized(); return _aoeSpenderName; } }

    private string _stSpenderName = Spells.GlacialBlast.Name;
    private string _aoeSpenderName = Spells.IceComet.Name;

    private Combatants Combatants => _combatants ??= Owner.GetModule<Combatants>()!;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnWintersEmbraceApplied(ApplyBuffEvent applyBuffEvent)
    {
        CloseWindow(applyBuffEvent.Timestamp, boundaryTruncated: false);

        _currentWindow = new WindowCapture
        {
            StartTimestamp = applyBuffEvent.Timestamp,
            EndTimestamp = applyBuffEvent.Timestamp,
            OrbsBankedAtOpen = _lastBurstingIce?.OrbsBanked,
            BurstingIceTarget = _lastBurstingIce?.Target,
            MajorsActiveAtOpen = SampleMajors(applyBuffEvent.Timestamp),
        };
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnWintersEmbraceRemoved(RemoveBuffEvent removeBuffEvent) =>
        CloseWindow(removeBuffEvent.Timestamp, boundaryTruncated: false);

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        TallySpender(castEvent.Ability.Id);

        if (castEvent.Ability.Id == Spells.BurstingIce.FSLID)
        {
            _lastBurstingIce = new BurstingIceAnchor(
                new UnitKey(castEvent.TargetId, castEvent.TargetInstance),
                ReadWinterOrbs(castEvent));
            return;
        }

        if (_currentWindow is not null && castEvent.GlobalCooldown is not null)
            _currentWindow.CastsInWindow.Add(castEvent);
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.BurstingIceDamage))]
    private void OnBurstingIceDamage(DamageEvent damageEvent) =>
        _currentWindow?.UniqueBurstingIceTargets.Add(new UnitKey(damageEvent.TargetId, damageEvent.TargetInstance));

    [On<DeathEvent>]
    private void OnDeath(DeathEvent deathEvent)
    {
        if (_currentWindow is not { BurstingIceTarget: { } target }) return;
        if (!SameUnit(target, new UnitKey(deathEvent.TargetId, deathEvent.TargetInstance))) return;

        _currentWindow.EndedByTargetDeath = true;
    }

    /// <summary>
    /// Matches two unit identities, treating a missing spawn instance on either side as a wildcard so
    /// a death logged without an instance still resolves to the tracked spawn.
    /// </summary>
    private static bool SameUnit(UnitKey tracked, UnitKey observed) =>
        tracked.ActorId == observed.ActorId &&
        (tracked.Instance is null || observed.Instance is null || tracked.Instance == observed.Instance);

    private IReadOnlyList<string> SampleMajors(int timestamp)
    {
        var majors = new List<string>(2);

        if (Combatants.Selected.HasBuff(Spells.IceBlitzBuff.FSLID, forTimestamp: timestamp))
            majors.Add(Spells.IceBlitzBuff.Name);

        if (Combatants.Selected.HasBuff(Spells.WrathOfWinterBuff.FSLID, forTimestamp: timestamp))
            majors.Add(Spells.WrathOfWinterBuff.Name);

        return majors;
    }

    private static int? ReadWinterOrbs(CastEvent castEvent)
    {
        var resources = castEvent.SourceResources?.Resources;
        if (resources is null) return null;

        foreach (var resource in resources)
        {
            if (resource.Type == ResourceTypes.Tertiary)
                return resource.Amount;
        }

        return null;
    }

    private void CloseWindow(int timestamp, bool boundaryTruncated)
    {
        if (_currentWindow is null) return;

        _currentWindow.EndTimestamp = Math.Max(_currentWindow.StartTimestamp, timestamp);
        _currentWindow.BoundaryTruncated = boundaryTruncated;
        _windows.Add(_currentWindow);
        _currentWindow = null;
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
    /// Detects the Winter Orb spender loadout. Icy Talons is a talent, so the combatant's own talent
    /// list decides it whenever the log carries combatant info; older logs that predate the talent
    /// fall back to comparing how often each spender pair was actually cast.
    /// </summary>
    private RimeBuild DetectBuild()
    {
        var combatant = Owner.SelectedCombatant;
        if (combatant.Talents.Count > 0)
            return combatant.HasTalent(RimeTalents.IcyTalons) ? RimeBuild.IcyTalons : RimeBuild.Default;

        var talons = _spenderCastCounts.GetValueOrDefault(Spells.TalonStrike.Id)
            + _spenderCastCounts.GetValueOrDefault(Spells.RisingTalons.Id);
        var frostfire = _spenderCastCounts.GetValueOrDefault(Spells.GlacialBlast.Id)
            + _spenderCastCounts.GetValueOrDefault(Spells.IceComet.Id);

        return talons > frostfire ? RimeBuild.IcyTalons : RimeBuild.Default;
    }

    /// <summary>Closes any window left open at the pull boundary, detects the build, and scores every capture, once on first read.</summary>
    private void EnsureMaterialized()
    {
        if (_materialized) return;
        _materialized = true;

        if (_currentWindow is not null)
        {
            var start = _currentWindow.StartTimestamp;
            CloseWindow(Math.Max(start, Math.Min(start + BaseWindowDurationMs, Pull.EndTime)), boundaryTruncated: true);
        }

        _build = DetectBuild();
        _stSpenderId = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Id : Spells.GlacialBlast.Id;
        _aoeSpenderId = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Id : Spells.IceComet.Id;
        _stSpenderName = _build == RimeBuild.IcyTalons ? Spells.TalonStrike.Name : Spells.GlacialBlast.Name;
        _aoeSpenderName = _build == RimeBuild.IcyTalons ? Spells.RisingTalons.Name : Spells.IceComet.Name;

        foreach (var window in _windows)
            _evaluations.Add(Evaluate(window));
    }

    private EmbraceWindowEvaluation Evaluate(WindowCapture window)
    {
        var castsInWindow = GetWindowCasts(window);
        var relevantCasts = GetRelevantCasts(castsInWindow);
        var evaluation = EvaluateWindow(window, castsInWindow, relevantCasts);

        evaluation.StartTimestamp = window.StartTimestamp;
        evaluation.EndTimestamp = window.EndTimestamp;
        evaluation.BoundaryTruncated = window.BoundaryTruncated;
        evaluation.EndedByTargetDeath = window.EndedByTargetDeath;
        evaluation.OrbsBankedAtOpen = window.OrbsBankedAtOpen;
        evaluation.MajorsActiveAtOpen = window.MajorsActiveAtOpen;
        evaluation.TargetCount = window.UniqueBurstingIceTargets.Count;
        evaluation.CastsInWindow = castsInWindow;
        evaluation.Build = _build;
        evaluation.StSpenderName = _stSpenderName;
        evaluation.AoeSpenderName = _aoeSpenderName;
        evaluation.StSpenderCount = relevantCasts.Count(IsStSpender);
        evaluation.AoeSpenderCount = relevantCasts.Count(IsAoeSpender);
        evaluation.ColdSnapCount = relevantCasts.Count(IsColdSnap);
        evaluation.FreezingTorrentCount = relevantCasts.Count(IsFreezingTorrent);

        return evaluation;
    }

    /// <summary>
    /// Scores one captured window against the follow-up this analyzer's pull shape expects. The
    /// shared facts are filled in by the base afterwards, so an override sets only its own members
    /// plus <see cref="EmbraceWindowEvaluation.Successful"/> and
    /// <see cref="EmbraceWindowEvaluation.Partial"/>.
    /// </summary>
    protected abstract EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        IReadOnlyList<CastEvent> castsInWindow,
        IReadOnlyList<CastEvent> relevantCasts);

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

    /// <summary>The Bursting Ice cast that opened a window: the enemy it landed on and the Winter Orbs banked as it went out.</summary>
    private sealed record BurstingIceAnchor(UnitKey Target, int? OrbsBanked);

    /// <summary>One Winter's Embrace window as captured during dispatch, before scoring.</summary>
    public sealed class WindowCapture
    {
        public int StartTimestamp { get; init; }
        public int EndTimestamp { get; set; }

        /// <summary>True when the window was still open at pull end and was closed at the boundary rather than by a logged removal.</summary>
        public bool BoundaryTruncated { get; set; }

        /// <summary>True when the enemy the opening Bursting Ice landed on died while the window was open.</summary>
        public bool EndedByTargetDeath { get; set; }

        /// <summary>Winter Orbs banked at the opening Bursting Ice cast; null when the log carries no resource snapshot.</summary>
        public int? OrbsBankedAtOpen { get; init; }

        /// <summary>The enemy the opening Bursting Ice landed on; null when no Bursting Ice cast preceded the window.</summary>
        public UnitKey? BurstingIceTarget { get; init; }

        /// <summary>Names of the major damage buffs already standing when the window opened.</summary>
        public IReadOnlyList<string> MajorsActiveAtOpen { get; init; } = [];

        public List<CastEvent> CastsInWindow { get; } = [];
        public HashSet<UnitKey> UniqueBurstingIceTargets { get; } = [];
    }

    /// <summary>
    /// The scored outcome of one Winter's Embrace window: the members every window shares regardless
    /// of pull shape. Each concrete analyzer returns its own subtype carrying the shape-specific
    /// detail.
    /// </summary>
    public abstract class EmbraceWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int EndTimestamp { get; set; }

        /// <summary>How long the window actually stood, in milliseconds.</summary>
        public int DurationMs => Math.Max(EndTimestamp - StartTimestamp, 0);

        /// <summary>True when the pull ended before the window did, so its length and follow-up are clipped.</summary>
        public bool BoundaryTruncated { get; set; }

        public RimeBuild Build { get; set; }
        public string StSpenderName { get; set; } = Spells.GlacialBlast.Name;
        public string AoeSpenderName { get; set; } = Spells.IceComet.Name;

        /// <summary>Winter Orbs banked at the opening Bursting Ice cast; null when the log carries no resource snapshot.</summary>
        public int? OrbsBankedAtOpen { get; set; }

        /// <summary>True when the window opened with too few orbs banked to spend into it.</summary>
        public bool OpenedStarved => OrbsBankedAtOpen is >= 0 and <= StarvedOrbThreshold;

        /// <summary>True when the Bursting Ice target died mid-window, ending Winter's Embrace early.</summary>
        public bool EndedByTargetDeath { get; set; }

        /// <summary>Names of the major damage buffs standing when the window opened.</summary>
        public IReadOnlyList<string> MajorsActiveAtOpen { get; set; } = [];

        /// <summary>True when at least one major damage buff was stacked onto this window.</summary>
        public bool MajorActiveAtOpen => MajorsActiveAtOpen.Count > 0;

        /// <summary>Unique enemies hit by Bursting Ice damage during the window.</summary>
        public int TargetCount { get; set; }

        public IReadOnlyList<CastEvent> CastsInWindow { get; set; } = [];

        public int StSpenderCount { get; set; }
        public int AoeSpenderCount { get; set; }
        public int ColdSnapCount { get; set; }
        public int FreezingTorrentCount { get; set; }

        public bool Successful { get; set; }
        public bool Partial { get; set; }

        /// <summary>The spender casts this pull shape counts, whichever spender that is.</summary>
        public abstract int SpenderCount { get; }

        /// <summary>How many spender casts this window should have fitted.</summary>
        public abstract int ExpectedSpenderCount { get; }

        /// <summary>The name of the spender this pull shape counts.</summary>
        public abstract string SpenderName { get; }
    }
}
