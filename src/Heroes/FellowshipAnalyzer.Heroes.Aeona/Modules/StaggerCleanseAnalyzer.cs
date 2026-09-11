using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>One ally's share of a cleanse cast: the healing on them and the Stagger the cast removed from them.</summary>
/// <param name="UnitId">The healed ally.</param>
/// <param name="IsTank">Whether that ally is the party's tank.</param>
/// <param name="EffectiveHealing">Effective healing on this ally.</param>
/// <param name="Overheal">Overheal on this ally.</param>
/// <param name="StaggerCleansed">The Stagger the cast removed from this ally, in hit points. Null when the cast cannot be bracketed, when something else moved the pool inside the bracket, or when the pool grew.</param>
/// <param name="StaggerBefore">The ally's Stagger no more than <see cref="StaggerTracker.StaggerMaxAgeMs"/> before the cast, in hit points. Null when nothing that recent precedes it.</param>
public sealed record CleanseHeal(
    int UnitId,
    bool IsTank,
    long EffectiveHealing,
    long Overheal,
    int? StaggerCleansed,
    int? StaggerBefore);

/// <summary>One Amend Fate or Restore Continuity cast, rated as a GCD against the Stagger removed and against an Oblivion.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
/// <param name="Heals">The cast's heals, one per ally, in the order the log reported them.</param>
/// <param name="StaggerBefore">The rated ally's Stagger before the cast, in hit points. Null when nothing recent enough precedes it.</param>
/// <param name="StaggerRemoved">The Stagger this ability removes, or null when the report holds no clean cast to take it from.</param>
/// <param name="TankStaggerFraction">The tank's Stagger as a share of its maximum health at the cast, or null when nothing recent enough precedes it.</param>
/// <param name="FreeCastSource">What made the cast free, or null when it cost Chrona.</param>
/// <param name="AppliedEchoes">Whether this cast applied Echoes of Divinity to the tank.</param>
/// <param name="OverwroteEchoes">Whether this cast refreshed Echoes of Divinity already running on the tank.</param>
/// <param name="EchoesOverwrittenMs">Echoes of Divinity time the refresh discarded, in milliseconds. Null when the cast refreshed nothing.</param>
/// <param name="OblivionValue">Effective healing plus shield absorb per Oblivion cast in this pull, or null with no Oblivion cast.</param>
/// <param name="EntropyClaimReadyInMs">Milliseconds until an Entropy's Claim charge was available at the cast; 0 when one was.</param>
/// <param name="StaggerIntakePerSecond">The tank's Stagger intake per second over the Entropy's Claim duration before the cast, or null with no tank.</param>
/// <param name="ProjectedStaggerFraction">The tank's Stagger as a share of maximum health at the next Entropy's Claim charge, holding the intake rate, or null when either figure is missing.</param>
public sealed record CleanseCastEntry(
    int Timestamp,
    FSLID Ability,
    IReadOnlyList<CleanseHeal> Heals,
    int? StaggerBefore,
    int? StaggerRemoved,
    double? TankStaggerFraction,
    FreeCastSource? FreeCastSource,
    bool AppliedEchoes,
    bool OverwroteEchoes,
    int? EchoesOverwrittenMs,
    double? OblivionValue,
    int EntropyClaimReadyInMs,
    double? StaggerIntakePerSecond,
    double? ProjectedStaggerFraction)
{
    /// <summary>The Stagger the cast removed across every ally, in hit points. Null when no ally's pool could be bracketed.</summary>
    public int? StaggerCleansed =>
        Heals.Any(heal => heal.StaggerCleansed is not null) ? Heals.Sum(heal => heal.StaggerCleansed ?? 0) : null;

    /// <summary>Effective healing across the cast's allies.</summary>
    public long EffectiveHealing => Heals.Sum(heal => heal.EffectiveHealing);

    /// <summary>Overheal across the cast's allies.</summary>
    public long Overheal => Heals.Sum(heal => heal.Overheal);

    /// <summary>Allies the cast healed.</summary>
    public int AlliesHealed => Heals.Count;

    /// <summary>Whether the cast cost no Chrona.</summary>
    public bool WasFree => FreeCastSource is not null;

    /// <summary>Whether the rated ally held less than the Stagger removed. Null when either figure is missing.</summary>
    public bool? BelowStaggerRemoved =>
        StaggerBefore is { } before && StaggerRemoved is { } amount ? before < amount : null;

    /// <summary>Whether the cast's effective healing was below an Oblivion's. Null with no Oblivion cast in the pull.</summary>
    public bool? BelowOblivionValue => OblivionValue is { } value ? EffectiveHealing < value : null;

    /// <summary>Whether the cast could be rated.</summary>
    public bool Rated => TankStaggerFraction is not null && (BelowStaggerRemoved is not null || BelowOblivionValue is not null);

    /// <summary>
    /// Whether the cast was a GCD spent below the cleanse priority on less than a cleanse or an Oblivion
    /// returns. A free cast that did not refresh a running Echoes of Divinity is never flagged.
    /// </summary>
    public bool Flagged =>
        Rated
        && TankStaggerFraction < OblivionAnalyzer.CleansePriorityStaggerFraction
        && !(WasFree && !OverwroteEchoes)
        && (BelowStaggerRemoved == true || BelowOblivionValue == true);

    /// <summary>
    /// Whether the tank would still have been under <see cref="StaggerCleanseAnalyzer.EntropicBurstHoldStaggerFraction"/>
    /// at the next Entropy's Claim charge, so the cleanse could have waited for an Oblivion.
    /// </summary>
    public bool CouldHaveWaited =>
        TankStaggerFraction < OblivionAnalyzer.CleansePriorityStaggerFraction
        && EntropyClaimReadyInMs > 0
        && ProjectedStaggerFraction is { } projected
        && projected < StaggerCleanseAnalyzer.EntropicBurstHoldStaggerFraction;
}

/// <summary>Echoes of Divinity on the tank across one pull.</summary>
/// <param name="Windows">The buff's windows on the tank, in the order they opened.</param>
/// <param name="Applications">Fresh applications on the tank.</param>
/// <param name="Refreshes">Refreshes over a window already running on the tank.</param>
/// <param name="Overwrites">Refreshes by a cleanse cast.</param>
/// <param name="OverwrittenMs">Milliseconds of running window those refreshes discarded.</param>
/// <param name="ActiveMs">Milliseconds the buff was on the tank, counting overlap once.</param>
/// <param name="Uptime">Share of the pull the buff was on the tank, from 0 to 1.</param>
public sealed record EchoesOfDivinityUse(
    IReadOnlyList<AuraWindow> Windows,
    int Applications,
    int Refreshes,
    int Overwrites,
    int OverwrittenMs,
    int ActiveMs,
    double Uptime);

/// <summary>
/// Amend Fate and Restore Continuity over one pull: each cast as a GCD, rated against the Stagger removed
/// and against the pull's Oblivion value; the Entropy's Claim charge it could have waited for; and
/// Echoes of Divinity on the tank.
/// </summary>
/// <remarks>
/// <para>
/// The Stagger a cast removed is the fall in the target's pool across the cast, from
/// <see cref="StaggerTracker.MeasureCleanse"/>. The Stagger removed is
/// <see cref="StaggerTracker.StaggerRemoved"/>, the median clean cast of that ability across the report.
/// </para>
/// <para>
/// The Oblivion value is effective healing plus Oblivion's Embrace absorb per Oblivion cast in this pull,
/// from Oblivion's own heal and shield events, credited to the most recent Oblivion cast within
/// <see cref="OblivionAnalyzer.AttributionMs"/>.
/// </para>
/// <para>
/// The projection holds the tank's intake rate over the last <see cref="AeonaBuild.EntropyClaimDurationMs"/>
/// for the time until the next charge and adds it to the tank's Stagger at the cast.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
[Dependency<SpellUsable>]
[Dependency<AeonaBuild>]
public sealed partial class StaggerCleanseAnalyzer : Analyzer
{
    /// <summary>The share of the tank's maximum health in Stagger the projection has to stay under.</summary>
    public const double EntropicBurstHoldStaggerFraction = 0.55;

    /// <summary>Milliseconds after a cleanse cast within which its heals and Echoes of Divinity events are credited to it.</summary>
    public const int HealAttributionWindowMs = 500;

    private readonly List<PendingCleanse> _pending = [];
    private readonly List<OblivionValueState> _oblivions = [];
    private readonly Dictionary<int, List<AuraWindow>> _echoesWindows = [];
    private readonly Dictionary<int, int> _echoesOpen = [];
    private readonly Dictionary<int, int> _echoesApplications = [];
    private readonly Dictionary<int, int> _echoesLastApplied = [];
    private readonly Dictionary<int, List<EchoesRefresh>> _echoesRefreshes = [];
    private readonly Dictionary<int, List<int>> _echoesFreshApplications = [];

    /// <summary>Every Amend Fate and Restore Continuity cast in the pull, in cast order.</summary>
    public IReadOnlyList<CleanseCastEntry> Casts => field ??= BuildCasts();

    /// <summary>Amend Fate casts in the pull.</summary>
    public int AmendFateCasts => CastsOf(Spells.AmendFate.FSLID);

    /// <summary>Restore Continuity casts in the pull.</summary>
    public int RestoreContinuityCasts => CastsOf(Spells.RestoreContinuity.FSLID);

    /// <summary>Effective healing from both cleanses across the pull.</summary>
    public long EffectiveHealing => Casts.Sum(cast => cast.EffectiveHealing);

    /// <summary>Overheal from both cleanses across the pull.</summary>
    public long Overheal => Casts.Sum(cast => cast.Overheal);

    /// <summary>Casts that could be rated.</summary>
    public int CastsRated => Casts.Count(cast => cast.Rated);

    /// <summary>Flagged casts. Read it against <see cref="CastsRated"/>.</summary>
    public int FlaggedCasts => Casts.Count(cast => cast.Flagged);

    /// <summary>Casts while the rated ally held less than the Stagger removed.</summary>
    public int LowStaggerCasts => Casts.Count(cast => cast.BelowStaggerRemoved == true);

    /// <summary>Casts whose effective healing was below an Oblivion's.</summary>
    public int BelowOblivionValueCasts => Casts.Count(cast => cast.BelowOblivionValue == true);

    /// <summary>Cleanse casts that cost no Chrona.</summary>
    public int FreeCasts => Casts.Count(cast => cast.WasFree);

    /// <summary>Free casts spent on Restore Continuity.</summary>
    public int FreeCastsOnRestoreContinuity => Casts.Count(cast => cast.WasFree && cast.Ability == Spells.RestoreContinuity.FSLID);

    /// <summary>Free casts of any ability in the pull.</summary>
    public int FreeCastsInPull => FreeCastTracker.FreeCastsBetween(Pull.StartTime, Pull.EndTime).Count;

    /// <summary>Casts made with no Entropy's Claim charge available.</summary>
    public int CastsWithEntropyClaimOnCooldown => Casts.Count(cast => cast.EntropyClaimReadyInMs > 0);

    /// <summary>Casts that could have waited for the next Entropy's Claim charge.</summary>
    public int CastsCouldHaveWaited => Casts.Count(cast => cast.CouldHaveWaited);

    /// <summary>Effective healing plus shield absorb per Oblivion cast in the pull, or null with no Oblivion cast.</summary>
    public double? OblivionValuePerCast =>
        _oblivions.Count == 0 ? null : _oblivions.Sum(oblivion => oblivion.EffectiveHealing + oblivion.ShieldApplied) / (double)_oblivions.Count;

    /// <summary>The party's tank, or null when the report names none.</summary>
    public int? TankId => StaggerTracker.TankId;

    /// <summary>The Stagger <paramref name="ability"/> removed across the pull, in hit points, counting only the casts that could be bracketed.</summary>
    /// <param name="ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int StaggerCleansedBy(FSLID ability) =>
        Casts.Where(cast => cast.Ability == ability).Sum(cast => cast.StaggerCleansed ?? 0);

    /// <summary>Casts of <paramref name="ability"/> that could be bracketed.</summary>
    /// <param name="ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int BracketedCastsOf(FSLID ability) =>
        Casts.Count(cast => cast.Ability == ability && cast.StaggerCleansed is not null);

    /// <summary>Echoes of Divinity on the tank, or null when the talent is not taken or the report names no tank.</summary>
    public EchoesOfDivinityUse? EchoesOfDivinity
    {
        get
        {
            if (!Owner.SelectedCombatant.HasTalent(AeonaTalents.EchoesOfDivinity)) return null;
            if (TankId is not { } tankId) return null;

            var windows = WindowsFor(tankId);
            var refreshes = _echoesRefreshes.TryGetValue(tankId, out var recorded) ? recorded : [];
            var activeMs = AuraWindowLedger.ActiveMs(windows);
            var overwriting = Casts.Where(cast => cast.OverwroteEchoes).ToList();

            return new EchoesOfDivinityUse(
                windows,
                _echoesApplications.GetValueOrDefault(tankId),
                refreshes.Count,
                overwriting.Count,
                overwriting.Sum(cast => cast.EchoesOverwrittenMs ?? 0),
                activeMs,
                Pull.Duration <= 0 ? 0 : (double)activeMs / Pull.Duration);
        }
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e)
    {
        var readyIn = SpellUsable.IsAvailable(Spells.EntropyClaim.FSLID)
            ? 0
            : Math.Max(0, SpellUsable.CooldownRemaining(Spells.EntropyClaim.FSLID, e.Timestamp));

        _pending.Add(new PendingCleanse(e.Timestamp, e.Ability.Id, readyIn));
    }

    [On<HealEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseHeal(HealEvent e)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (pending.Ability != e.Ability.Id) continue;
            if (e.Timestamp - pending.Timestamp > HealAttributionWindowMs) return;

            pending.Heals.Add(e);
            return;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionCast(CastEvent e) => _oblivions.Add(new OblivionValueState(e.Timestamp));

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionHeal(HealEvent e)
    {
        if (CurrentOblivion(e.Timestamp) is { } oblivion) oblivion.EffectiveHealing += e.Amount;
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnOblivionShieldApplied(ApplyBuffEvent e)
    {
        if (CurrentOblivion(e.Timestamp) is { } oblivion) oblivion.ShieldApplied += e.Absorb ?? 0;
    }

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnOblivionShieldRefreshed(RefreshBuffEvent e)
    {
        if (CurrentOblivion(e.Timestamp) is { } oblivion) oblivion.ShieldApplied += e.Absorb ?? 0;
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesApplied(ApplyBuffEvent e)
    {
        _echoesOpen.TryAdd(e.TargetId, e.Timestamp);
        _echoesApplications[e.TargetId] = _echoesApplications.GetValueOrDefault(e.TargetId) + 1;
        _echoesLastApplied[e.TargetId] = e.Timestamp;

        if (!_echoesFreshApplications.TryGetValue(e.TargetId, out var fresh))
            _echoesFreshApplications[e.TargetId] = fresh = [];

        fresh.Add(e.Timestamp);
    }

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesRefreshed(RefreshBuffEvent e)
    {
        _echoesOpen.TryAdd(e.TargetId, e.Timestamp);

        if (!_echoesRefreshes.TryGetValue(e.TargetId, out var recorded))
            _echoesRefreshes[e.TargetId] = recorded = [];

        recorded.Add(new EchoesRefresh(
            e.Timestamp,
            _echoesLastApplied.TryGetValue(e.TargetId, out var lastApplied) ? lastApplied : null));

        _echoesLastApplied[e.TargetId] = e.Timestamp;
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesRemoved(RemoveBuffEvent e)
    {
        if (!_echoesOpen.Remove(e.TargetId, out var start)) return;

        if (!_echoesWindows.TryGetValue(e.TargetId, out var closed))
            _echoesWindows[e.TargetId] = closed = [];

        closed.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
    }

    private OblivionValueState? CurrentOblivion(int timestamp) =>
        _oblivions.Count > 0
        && timestamp >= _oblivions[^1].Timestamp
        && timestamp - _oblivions[^1].Timestamp <= OblivionAnalyzer.AttributionMs
            ? _oblivions[^1]
            : null;

    /// <summary>
    /// How long one application of Echoes of Divinity runs, taken from the report: the longest window on
    /// the tank that closed on a removal with no refresh inside it. Null until the report shows one.
    /// </summary>
    private int? EchoesDurationMs
    {
        get
        {
            if (TankId is not { } tank) return null;
            if (!_echoesWindows.TryGetValue(tank, out var windows)) return null;

            var refreshes = _echoesRefreshes.TryGetValue(tank, out var recorded) ? recorded : [];
            int? longest = null;

            foreach (var window in windows)
            {
                if (refreshes.Any(refresh => refresh.Timestamp > window.Start && refresh.Timestamp < window.End))
                    continue;

                var length = window.End - window.Start;
                if (length > (longest ?? 0)) longest = length;
            }

            return longest;
        }
    }

    private List<AuraWindow> WindowsFor(int unitId)
    {
        var result = _echoesWindows.TryGetValue(unitId, out var closed) ? [.. closed] : new List<AuraWindow>();

        if (_echoesOpen.TryGetValue(unitId, out var start))
            result.Add(new AuraWindow(start, Math.Max(start, Pull.EndTime)));

        return result;
    }

    private int CastsOf(FSLID ability) => Casts.Count(cast => cast.Ability == ability);

    private List<CleanseCastEntry> BuildCasts()
    {
        var tankId = TankId;
        var casts = new List<CleanseCastEntry>(_pending.Count);
        var refreshesByCast = RefreshesByCast(tankId);
        var oblivionValue = OblivionValuePerCast;
        var lookback = AeonaBuild.EntropyClaimDurationMs;

        foreach (var pending in _pending)
        {
            List<CleanseHeal> heals = [];
            foreach (var heal in pending.Heals)
                heals.Add(BuildHeal(heal, pending.Timestamp, tankId));

            var rated = RatedHeal(heals);
            var tankFraction = tankId is { } tank
                ? StaggerTracker.StaggerFractionOfMaxHp(tank, pending.Timestamp, StaggerTracker.StaggerMaxAgeMs)
                : null;
            var intake = tankId is { } intakeTank ? StaggerTracker.IntakePerSecond(intakeTank, pending.Timestamp, lookback) : (double?)null;
            var maxHitPoints = tankId is { } hpTank ? StaggerTracker.MaxHitPointsOf(hpTank, pending.Timestamp) : null;
            var projected = tankFraction is { } fraction && intake is { } rate && maxHitPoints is { } maxHp && maxHp > 0
                ? fraction + rate * pending.EntropyClaimReadyInMs / 1000d / maxHp
                : (double?)null;
            var overwritten = refreshesByCast.TryGetValue(pending.Timestamp, out var discarded) ? discarded : null;
            var overwrote = refreshesByCast.ContainsKey(pending.Timestamp);

            casts.Add(new CleanseCastEntry(
                pending.Timestamp,
                pending.Ability,
                heals,
                rated?.StaggerBefore,
                StaggerTracker.StaggerRemoved(pending.Ability),
                tankFraction,
                FreeCastTracker.FreeCastAt(pending.Timestamp, pending.Ability)?.Source,
                AppliedEchoesAt(tankId, pending.Timestamp),
                overwrote,
                overwrote ? overwritten : null,
                oblivionValue,
                pending.EntropyClaimReadyInMs,
                intake,
                projected));
        }

        return casts;
    }

    private bool AppliedEchoesAt(int? tankId, int castTimestamp) =>
        tankId is { } tank
        && _echoesFreshApplications.TryGetValue(tank, out var fresh)
        && fresh.Any(applied => applied >= castTimestamp && applied - castTimestamp <= HealAttributionWindowMs);

    /// <summary>The Echoes of Divinity time each cleanse cast's refresh discarded, keyed by the cast's timestamp.</summary>
    private Dictionary<int, int?> RefreshesByCast(int? tankId)
    {
        var byCast = new Dictionary<int, int?>();
        if (tankId is not { } tank || !_echoesRefreshes.TryGetValue(tank, out var refreshes)) return byCast;

        var duration = EchoesDurationMs;

        foreach (var refresh in refreshes)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                if (pending.Timestamp > refresh.Timestamp) continue;
                if (refresh.Timestamp - pending.Timestamp > HealAttributionWindowMs) break;

                var running = byCast.GetValueOrDefault(pending.Timestamp);
                byCast[pending.Timestamp] = refresh.RemainingMs(duration) is { } remaining
                    ? (running ?? 0) + remaining
                    : running;
                break;
            }
        }

        return byCast;
    }

    /// <summary>The ally a cast is rated on: the healed ally holding the most Stagger before the cast.</summary>
    private static CleanseHeal? RatedHeal(List<CleanseHeal> heals)
    {
        CleanseHeal? rated = null;
        foreach (var heal in heals)
        {
            if (heal.StaggerBefore is not { } before) continue;
            if (rated?.StaggerBefore is { } best && best >= before) continue;

            rated = heal;
        }

        return rated;
    }

    private CleanseHeal BuildHeal(HealEvent heal, int castTimestamp, int? tankId)
    {
        var measurement = StaggerTracker.MeasureCleanse(heal.TargetId, castTimestamp, StaggerTracker.CleanseBracketWindowMs);
        var cleansed = measurement is { HasInterveningEvent: false, ClearedAmount: > 0 }
            ? measurement.ClearedAmount
            : (int?)null;

        var before = StaggerTracker.LatestBefore(heal.TargetId, castTimestamp);
        var fresh = before is not null && castTimestamp - before.Timestamp <= StaggerTracker.StaggerMaxAgeMs;

        return new CleanseHeal(
            heal.TargetId,
            tankId == heal.TargetId,
            heal.Amount,
            heal.Overheal ?? 0,
            cleansed,
            fresh ? before!.Amount : null);
    }

    private readonly record struct EchoesRefresh(int Timestamp, int? PreviousApplication)
    {
        public int? RemainingMs(int? durationMs) =>
            PreviousApplication is { } previous && durationMs is { } duration
                ? Math.Max(0, previous + duration - Timestamp)
                : null;
    }

    private sealed class PendingCleanse(int timestamp, int ability, int entropyClaimReadyInMs)
    {
        public int Timestamp { get; } = timestamp;

        public int Ability { get; } = ability;

        public int EntropyClaimReadyInMs { get; } = entropyClaimReadyInMs;

        public List<HealEvent> Heals { get; } = [];
    }

    private sealed class OblivionValueState(int timestamp)
    {
        public int Timestamp { get; } = timestamp;

        public long EffectiveHealing { get; set; }

        public long ShieldApplied { get; set; }
    }
}
