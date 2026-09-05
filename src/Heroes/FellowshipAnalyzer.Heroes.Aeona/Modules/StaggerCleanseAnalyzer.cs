using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// One ally's share of a cleanse cast: the healing that landed on them and the Stagger the cast took
/// off them.
/// </summary>
/// <param name="UnitId">The healed ally.</param>
/// <param name="IsTank">Whether that ally is the party's tank.</param>
/// <param name="EffectiveHealing">Healing that landed on missing hit points.</param>
/// <param name="Overheal">Healing that landed on full hit points. Fellowship omits the field rather than sending zero, so a cast with none reads 0 here.</param>
/// <param name="StaggerCleansed">The Stagger the cast took off this ally, in hit points, from the ally's readings either side of the cast. Null when the readings do not bracket the cast, when something else moved the pool inside the bracket, or when the pool grew.</param>
/// <param name="StaggerBefore">The ally's Stagger at the last reading within <see cref="StaggerTracker.ReadingMaxAgeMs"/> of the cast, in hit points. Null when no reading is that fresh.</param>
public sealed record CleanseHeal(
    int UnitId,
    bool IsTank,
    long EffectiveHealing,
    long Overheal,
    int? StaggerCleansed,
    int? StaggerBefore);

/// <summary>
/// One Amend Fate or Restore Continuity cast, with every heal it produced, the Stagger it cleared and
/// how the pool it was aimed at compared with what one cast removes.
/// </summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
/// <param name="Heals">The cast's heals, one per ally, in the order the log reported them.</param>
/// <param name="StaggerBefore">The judged ally's Stagger before the cast, in hit points, or null when no reading is fresh enough.</param>
/// <param name="SingleCastCleanseAmount">The Stagger one cast of this ability removes, or null when the report holds no clean cast to take it from.</param>
/// <param name="WasFree">Whether Fellowship logged the cast as a free cast rather than a paid one.</param>
/// <param name="FreeCastSource">What paid for a free cast. Null on a paid cast.</param>
/// <param name="OverwroteEchoes">Whether this low-Stagger cast reapplied Echoes of Divinity over a window already running on the tank.</param>
/// <param name="EchoesOverwrittenMs">Echoes of Divinity time that reapplication discarded, in milliseconds. Null when the cast overwrote nothing and when the game data states no duration for the effect.</param>
public sealed record CleanseCastEntry(
    int Timestamp,
    FSLID Ability,
    IReadOnlyList<CleanseHeal> Heals,
    int? StaggerBefore,
    int? SingleCastCleanseAmount,
    bool WasFree,
    FreeCastSource? FreeCastSource,
    bool OverwroteEchoes,
    int? EchoesOverwrittenMs)
{
    /// <summary>
    /// The Stagger the cast removed across every ally whose readings bracket it, in hit points, or
    /// null when no ally's readings do.
    /// </summary>
    public int? StaggerCleansed =>
        Heals.Any(heal => heal.StaggerCleansed is not null)
            ? Heals.Sum(heal => heal.StaggerCleansed ?? 0)
            : null;

    /// <summary>Healing the cast landed on missing hit points.</summary>
    public long EffectiveHealing => Heals.Sum(heal => heal.EffectiveHealing);

    /// <summary>Healing the cast landed on full hit points.</summary>
    public long Overheal => Heals.Sum(heal => heal.Overheal);

    /// <summary>Allies the cast healed. Amend Fate reaches one; Restore Continuity reaches the party.</summary>
    public int AlliesHealed => Heals.Count;

    /// <summary>
    /// Whether the judged ally held less Stagger than one cast of this ability removes, which is the
    /// doc's priority test. Null when no reading is fresh enough to judge, or when the report holds no
    /// clean cast to take the cleanse amount from.
    /// </summary>
    public bool? BelowSingleCleanse =>
        StaggerBefore is { } before && SingleCastCleanseAmount is { } amount ? before < amount : null;

    /// <summary>
    /// Whether a free cast was spent on a pool deep enough to take a whole cleanse. Null on a paid
    /// cast and on a free cast the readings cannot judge.
    /// </summary>
    public bool? FreeCastOnFullPool => WasFree ? BelowSingleCleanse is { } below ? !below : null : null;
}

/// <summary>
/// Echoes of Divinity on the tank across one pull: how long the talent's Stagger cleanse ran, and how
/// much of a running window a fresh low-Stagger cleanse wrote over.
/// </summary>
/// <param name="Windows">The buff's windows on the tank, in the order they opened.</param>
/// <param name="Applications">Fresh applications on the tank.</param>
/// <param name="Refreshes">Applications that landed on a window already running on the tank.</param>
/// <param name="Overwrites">Refreshes by a low-Stagger cleanse that cut a running window short.</param>
/// <param name="OverwrittenMs">Milliseconds of running window those overwrites discarded.</param>
/// <param name="GrantedMs">Milliseconds of Echoes of Divinity the applications and refreshes granted in total.</param>
/// <param name="ActiveMs">Milliseconds the buff was on the tank, counting overlap once.</param>
/// <param name="Uptime">Share of the pull the buff was on the tank, from 0 to 1.</param>
public sealed record EchoesOfDivinityUse(
    IReadOnlyList<AuraWindow> Windows,
    int Applications,
    int Refreshes,
    int Overwrites,
    int OverwrittenMs,
    int GrantedMs,
    int ActiveMs,
    double Uptime);

/// <summary>
/// Amend Fate and Restore Continuity: the Stagger each cast cleared, the healing that came with it,
/// and whether the pool held more Stagger than one cast removes.
/// <para>
/// Fellowship emits no cleanse event, so the Stagger a cast removed is the fall in the target's pool
/// across the cast, from <see cref="StaggerTracker.MeasureCleanse"/>. A bracket that a drain tick or
/// another cleanse also moved carries no figure, which is the doc's safeguard against intervening
/// ticks and cleanses.
/// </para>
/// <para>
/// What one cast removes is <see cref="StaggerTracker.SingleCastCleanseAmount"/>, the median clean
/// cast of that ability across the report, so the comparison is against the ability's own behaviour in
/// this report rather than against a modelled ceiling.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
public sealed partial class StaggerCleanseAnalyzer : Analyzer
{
    private const int HealAttributionWindowMs = 500;

    private readonly List<PendingCleanse> _pending = [];
    private readonly Dictionary<int, List<AuraWindow>> _echoesWindows = [];
    private readonly Dictionary<int, int> _echoesOpen = [];
    private readonly Dictionary<int, int> _echoesApplications = [];
    private readonly Dictionary<int, int> _echoesLastApplied = [];
    private readonly Dictionary<int, List<EchoesRefresh>> _echoesRefreshes = [];

    private List<CleanseCastEntry>? _casts;

    /// <summary>Every Amend Fate and Restore Continuity cast in the pull, in cast order.</summary>
    public IReadOnlyList<CleanseCastEntry> Casts => _casts ??= BuildCasts();

    /// <summary>Amend Fate casts in the pull.</summary>
    public int AmendFateCasts => CastsOf(Spells.AmendFate.FSLID);

    /// <summary>Restore Continuity casts in the pull.</summary>
    public int RestoreContinuityCasts => CastsOf(Spells.RestoreContinuity.FSLID);

    /// <summary>Healing both cleanses landed on missing hit points across the pull.</summary>
    public long EffectiveHealing => Casts.Sum(cast => cast.EffectiveHealing);

    /// <summary>Healing both cleanses landed on full hit points across the pull.</summary>
    public long Overheal => Casts.Sum(cast => cast.Overheal);

    /// <summary>Cleanse casts the readings could judge against the amount one cast removes.</summary>
    public int CastsWithStaggerReading => Casts.Count(cast => cast.BelowSingleCleanse is not null);

    /// <summary>
    /// Casts made while the target held less Stagger than one cast removes. Read it against
    /// <see cref="CastsWithStaggerReading"/>.
    /// </summary>
    public int LowStaggerCasts => Casts.Count(cast => cast.BelowSingleCleanse == true);

    /// <summary>Cleanse casts Fellowship logged as free.</summary>
    public int FreeCleanseCasts => Casts.Count(cast => cast.WasFree);

    /// <summary>Free cleanse casts the readings could judge against the amount one cast removes.</summary>
    public int FreeCleanseCastsWithStaggerReading =>
        Casts.Count(cast => cast.WasFree && cast.FreeCastOnFullPool is not null);

    /// <summary>Free cleanse casts spent on a pool holding at least the Stagger one cast removes.</summary>
    public int FreeCleanseCastsOnFullPool => Casts.Count(cast => cast.FreeCastOnFullPool == true);

    /// <summary>The party's tank, or null when the report names none.</summary>
    public int? TankId => StaggerTracker.TankId;

    /// <summary>
    /// The Stagger <paramref name="ability"/> removed across the pull, in hit points, counting only the
    /// casts whose readings bracket them.
    /// </summary>
    /// <param name="ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int StaggerCleansedBy(FSLID ability) =>
        Casts.Where(cast => cast.Ability == ability).Sum(cast => cast.StaggerCleansed ?? 0);

    /// <summary>
    /// Casts of <paramref name="ability"/> whose readings bracket them, the denominator behind
    /// <see cref="StaggerCleansedBy"/>.
    /// </summary>
    /// <param name="ability">Either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
    public int MeasuredCastsOf(FSLID ability) =>
        Casts.Count(cast => cast.Ability == ability && cast.StaggerCleansed is not null);

    /// <summary>
    /// Echoes of Divinity on the tank, or null when the talent is not taken or the report names no
    /// tank. Applications on other party members are excluded, so a party-wide Restore Continuity does
    /// not inflate the tank's figures.
    /// </summary>
    public EchoesOfDivinityUse? EchoesOfDivinity
    {
        get
        {
            if (!Owner.SelectedCombatant.HasTalent(AeonaTalents.EchoesOfDivinity)) return null;
            if (TankId is not { } tankId) return null;

            var windows = WindowsFor(_echoesWindows, _echoesOpen, tankId);
            var refreshes = _echoesRefreshes.TryGetValue(tankId, out var recorded) ? recorded : [];
            var activeMs = AuraWindowLedger.ActiveMs(windows);
            var applications = _echoesApplications.GetValueOrDefault(tankId);

            var overwriting = Casts.Where(cast => cast.OverwroteEchoes).ToList();

            return new EchoesOfDivinityUse(
                windows,
                applications,
                refreshes.Count,
                overwriting.Count,
                overwriting.Sum(cast => cast.EchoesOverwrittenMs ?? 0),
                (applications + refreshes.Count) * (EchoesDurationMs ?? 0),
                activeMs,
                Share(activeMs));
        }
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnCleanseCast(CastEvent e) => _pending.Add(new PendingCleanse(e.Timestamp, e.Ability.Id, false));

    [On<FreeCastEvent>(By = Actor.Player, Spells = [nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnFreeCleanseCast(FreeCastEvent e) => _pending.Add(new PendingCleanse(e.Timestamp, e.Ability.Id, true));

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

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesApplied(ApplyBuffEvent e)
    {
        Open(_echoesOpen, e.TargetId, e.Timestamp);
        _echoesApplications[e.TargetId] = _echoesApplications.GetValueOrDefault(e.TargetId) + 1;
        _echoesLastApplied[e.TargetId] = e.Timestamp;
    }

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesRefreshed(RefreshBuffEvent e)
    {
        Open(_echoesOpen, e.TargetId, e.Timestamp);

        if (!_echoesRefreshes.TryGetValue(e.TargetId, out var recorded))
            _echoesRefreshes[e.TargetId] = recorded = [];

        recorded.Add(new EchoesRefresh(
            e.Timestamp,
            _echoesLastApplied.TryGetValue(e.TargetId, out var lastApplied) ? lastApplied : null));

        _echoesLastApplied[e.TargetId] = e.Timestamp;
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfDivinity))]
    private void OnEchoesRemoved(RemoveBuffEvent e) => Close(_echoesWindows, _echoesOpen, e.TargetId, e.Timestamp);

    /// <summary>
    /// How long one application of Echoes of Divinity runs, taken from the report: the longest window on
    /// the tank that closed on a removal with no reapplication inside it, since a reapplication restarts
    /// the duration and every shorter window was cut off. Null until the report shows one such window.
    /// </summary>
    private int? EchoesDurationMs
    {
        get
        {
            if (StaggerTracker.TankId is not { } tank) return null;
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

    private static void Open(Dictionary<int, int> open, int unitId, int timestamp) => open.TryAdd(unitId, timestamp);

    private static void Close(Dictionary<int, List<AuraWindow>> windows, Dictionary<int, int> open, int unitId, int timestamp)
    {
        if (!open.Remove(unitId, out var start)) return;

        if (!windows.TryGetValue(unitId, out var closed))
            windows[unitId] = closed = [];

        closed.Add(new AuraWindow(start, Math.Max(start, timestamp)));
    }

    private List<AuraWindow> WindowsFor(Dictionary<int, List<AuraWindow>> windows, Dictionary<int, int> open, int unitId)
    {
        var result = windows.TryGetValue(unitId, out var closed) ? [.. closed] : new List<AuraWindow>();

        if (open.TryGetValue(unitId, out var start))
            result.Add(new AuraWindow(start, Math.Max(start, Pull.EndTime)));

        return result;
    }

    private double Share(int milliseconds) => Pull.Duration <= 0 ? 0 : (double)milliseconds / Pull.Duration;

    private int CastsOf(FSLID ability) => Casts.Count(cast => cast.Ability == ability);

    private List<CleanseCastEntry> BuildCasts()
    {
        var tankId = StaggerTracker.TankId;
        var casts = new List<CleanseCastEntry>(_pending.Count);
        var refreshesByCast = RefreshesByCast(tankId);

        foreach (var pending in _pending)
        {
            List<CleanseHeal> heals = [];
            foreach (var heal in pending.Heals)
                heals.Add(BuildHeal(heal, pending.Timestamp, tankId));

            var cleanseAmount = StaggerTracker.SingleCastCleanseAmount(pending.Ability);
            var judged = JudgedHeal(heals);
            var below = judged?.StaggerBefore is { } before && cleanseAmount is { } amount ? before < amount : (bool?)null;
            var hasRefresh = refreshesByCast.TryGetValue(pending.Timestamp, out var overwritten);
            var overwrote = below == true && hasRefresh;

            casts.Add(new CleanseCastEntry(
                pending.Timestamp,
                pending.Ability,
                heals,
                judged?.StaggerBefore,
                cleanseAmount,
                pending.WasFree,
                pending.WasFree ? FreeCastTracker.FreeCastAt(pending.Timestamp, pending.Ability)?.Source : null,
                overwrote,
                overwrote ? overwritten : null));
        }

        return casts;
    }

    /// <summary>
    /// The Echoes of Divinity time each cleanse cast wrote over, keyed by the cast's timestamp. Built
    /// before the cast list so a cast can carry its own figure.
    /// </summary>
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

    /// <summary>
    /// The ally a cast is judged on: the healed ally carrying the most Stagger before the cast, since
    /// that is the ally the cast was worth making for. Amend Fate heals one ally, so the rule picks
    /// that ally whenever a reading supports it.
    /// </summary>
    private static CleanseHeal? JudgedHeal(List<CleanseHeal> heals)
    {
        CleanseHeal? judged = null;
        foreach (var heal in heals)
        {
            if (heal.StaggerBefore is not { } before) continue;
            if (judged?.StaggerBefore is { } best && best >= before) continue;

            judged = heal;
        }

        return judged;
    }

    private CleanseHeal BuildHeal(HealEvent heal, int castTimestamp, int? tankId)
    {
        var measurement = StaggerTracker.MeasureCleanse(heal.TargetId, castTimestamp, StaggerTracker.CleanseBracketWindowMs);
        var cleansed = measurement is { HasInterveningEvent: false, ClearedAmount: > 0 }
            ? measurement.ClearedAmount
            : (int?)null;

        var before = StaggerTracker.LatestBefore(heal.TargetId, castTimestamp);
        var fresh = before is not null && castTimestamp - before.Timestamp <= StaggerTracker.ReadingMaxAgeMs;

        return new CleanseHeal(
            heal.TargetId,
            tankId == heal.TargetId,
            heal.Amount,
            heal.Overheal ?? 0,
            cleansed,
            fresh ? before!.Amount : null);
    }

    /// <summary>
    /// One reapplication of Echoes of Divinity on the tank, with the application it landed on so the time
    /// it discarded can be worked out once the report has shown how long one application runs.
    /// </summary>
    private readonly record struct EchoesRefresh(int Timestamp, int? PreviousApplication)
    {
        public int? RemainingMs(int? durationMs) =>
            PreviousApplication is { } previous && durationMs is { } duration
                ? Math.Max(0, previous + duration - Timestamp)
                : null;
    }

    private sealed class PendingCleanse(int timestamp, int ability, bool wasFree)
    {
        public int Timestamp { get; } = timestamp;

        public int Ability { get; } = ability;

        public bool WasFree { get; } = wasFree;

        public List<HealEvent> Heals { get; } = [];
    }
}
