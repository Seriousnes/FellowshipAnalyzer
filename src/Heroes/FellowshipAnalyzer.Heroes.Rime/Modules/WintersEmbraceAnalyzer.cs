using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using RimeTalents = FellowshipAnalyzer.Core.Common.Spells.RimeTalents;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public abstract partial class WintersEmbraceAnalyzer : Analyzer
{
    public const int BaseWindowDurationMs = 3000;

    public const int StarvedOrbThreshold = 1;

    private readonly List<WindowCapture> _windows = [];
    private readonly List<EmbraceWindowEvaluation> _evaluations = [];
    private readonly Dictionary<int, int> _spenderCastCounts = [];

    private WindowCapture? _currentWindow;
    private BurstingIceAnchor? _lastBurstingIce;
    private bool _materialized;
    private RimeBuild _build;
    private int _stSpenderId;
    private int _aoeSpenderId;

    public RimeBuild Build { get { EnsureMaterialized(); return _build; } }

    public IReadOnlyList<EmbraceWindowEvaluation> Windows { get { EnsureMaterialized(); return _evaluations; } }

    public int EvaluatedWindows => Windows.Count;

    public int SuccessfulWindows => Windows.Count(window => window.Successful);

    public int PartialWindows => Windows.Count(window => window.Partial);

    public int StarvedWindows => Windows.Count(window => window.OpenedStarved);

    public int WindowsEndedByTargetDeath => Windows.Count(window => window.EndedByTargetDeath);

    public int WindowsWithOrbData => Windows.Count(window => window.OrbsBankedAtOpen is not null);

    public string StSpenderName { get { EnsureMaterialized(); return _stSpenderName; } }

    public string AoeSpenderName { get { EnsureMaterialized(); return _aoeSpenderName; } }

    private string _stSpenderName = Spells.GlacialBlast.Name;
    private string _aoeSpenderName = Spells.IceComet.Name;

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

    private static bool SameUnit(UnitKey tracked, UnitKey observed) =>
        tracked.ActorId == observed.ActorId &&
        (tracked.Instance is null || observed.Instance is null || tracked.Instance == observed.Instance);

    private IReadOnlyList<string> SampleMajors(int timestamp)
    {
        var majors = new List<string>(2);

        if (Owner.SelectedCombatant.HasBuff(Spells.IceBlitzBuff.FSLID, forTimestamp: timestamp))
            majors.Add(Spells.IceBlitzBuff.Name);

        if (Owner.SelectedCombatant.HasBuff(Spells.WrathOfWinterBuff.FSLID, forTimestamp: timestamp))
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

    protected abstract EmbraceWindowEvaluation EvaluateWindow(
        WindowCapture window,
        IReadOnlyList<CastEvent> castsInWindow,
        IReadOnlyList<CastEvent> relevantCasts);

    protected static List<CastEvent> GetWindowCasts(WindowCapture window) =>
        [.. window.CastsInWindow.Where(c => c.Timestamp > window.StartTimestamp && c.Timestamp <= window.EndTimestamp)];

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

    private sealed record BurstingIceAnchor(UnitKey Target, int? OrbsBanked);

    public sealed class WindowCapture
    {
        public int StartTimestamp { get; init; }
        public int EndTimestamp { get; set; }

        public bool BoundaryTruncated { get; set; }

        public bool EndedByTargetDeath { get; set; }

        public int? OrbsBankedAtOpen { get; init; }

        public UnitKey? BurstingIceTarget { get; init; }

        public IReadOnlyList<string> MajorsActiveAtOpen { get; init; } = [];

        public List<CastEvent> CastsInWindow { get; } = [];
        public HashSet<UnitKey> UniqueBurstingIceTargets { get; } = [];
    }

    public abstract class EmbraceWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int EndTimestamp { get; set; }

        public int DurationMs => Math.Max(EndTimestamp - StartTimestamp, 0);

        public bool BoundaryTruncated { get; set; }

        public RimeBuild Build { get; set; }
        public string StSpenderName { get; set; } = Spells.GlacialBlast.Name;
        public string AoeSpenderName { get; set; } = Spells.IceComet.Name;

        public int? OrbsBankedAtOpen { get; set; }

        public bool OpenedStarved => OrbsBankedAtOpen is >= 0 and <= StarvedOrbThreshold;

        public bool EndedByTargetDeath { get; set; }

        public IReadOnlyList<string> MajorsActiveAtOpen { get; set; } = [];

        public bool MajorActiveAtOpen => MajorsActiveAtOpen.Count > 0;

        public int TargetCount { get; set; }

        public IReadOnlyList<CastEvent> CastsInWindow { get; set; } = [];

        public int StSpenderCount { get; set; }
        public int AoeSpenderCount { get; set; }
        public int ColdSnapCount { get; set; }
        public int FreezingTorrentCount { get; set; }

        public bool Successful { get; set; }
        public bool Partial { get; set; }

        public abstract int SpenderCount { get; }

        public abstract int ExpectedSpenderCount { get; }

        public abstract string SpenderName { get; }
    }
}
