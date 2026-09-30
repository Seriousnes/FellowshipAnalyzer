using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Continuum Shift.</summary>
public interface IContinuumShiftAnalyzer : IAnalyzerSurface;

/// <summary>What one Continuum Shift window was spent on.</summary>
public enum ContinuumShiftSpend
{
    /// <summary>An empowered Time Shard.</summary>
    TimeShard,

    /// <summary>An Echoes of Ruin applied to up to 20 enemies.</summary>
    EchoesOfRuin,

    /// <summary>A stunning Entropy's Claim.</summary>
    EntropyClaim,

    /// <summary>Removed with no cast beside it.</summary>
    Lost,

    /// <summary>Still active when the pull ended.</summary>
    OpenAtPullEnd,
}

/// <summary>One Continuum Shift window on the player and the cast that spent it.</summary>
/// <param name="Start">When the window opened.</param>
/// <param name="End">When the window closed, or the pull's end for a window that never closed.</param>
/// <param name="Spend">What the window was spent on.</param>
/// <param name="CastTimestamp">When the spending cast completed, or null when nothing spent it.</param>
/// <param name="Damage">Time Shard damage inside the cast's damage window.</param>
/// <param name="EffectiveHealing">Effective healing from the Time Shard inside the cast's damage window.</param>
/// <param name="Overheal">Overheal from the Time Shard inside the cast's damage window.</param>
/// <param name="ChronaOvercapped">Chrona lost at the maximum inside the cast's damage window.</param>
/// <param name="EnemiesApplied">Enemies the Echoes of Ruin was applied to.</param>
/// <param name="TankHealthFraction">The tank's health as a share of its maximum at the cast, or null when nothing precedes it.</param>
/// <param name="FleetingHourFollowed">Whether, with Edge of Ruin taken, Fleeting Hour was cast inside the Echoes of Ruin duration after the cast.</param>
public sealed record ContinuumShiftWindow(
    int Start,
    int End,
    ContinuumShiftSpend Spend,
    int? CastTimestamp,
    long Damage,
    long EffectiveHealing,
    long Overheal,
    int ChronaOvercapped,
    int EnemiesApplied,
    double? TankHealthFraction,
    bool FleetingHourFollowed)
{
    /// <summary>How long the window stayed open.</summary>
    public int DurationMs => End - Start;

    /// <summary>
    /// Whether the spend was the right one: a Time Shard always; an Echoes of Ruin with the tank at or
    /// under <see cref="ContinuumShiftAnalyzer.TankDangerHealthFraction"/> or with Fleeting Hour following
    /// under Edge of Ruin.
    /// </summary>
    public bool Justified => Spend switch
    {
        ContinuumShiftSpend.TimeShard => true,
        ContinuumShiftSpend.EchoesOfRuin =>
            TankHealthFraction <= ContinuumShiftAnalyzer.TankDangerHealthFraction || FleetingHourFollowed,
        _ => false,
    };
}

/// <summary>
/// Continuum Shift over one pull: every window, the empowered cast that spent it, and what that cast
/// produced.
/// </summary>
/// <remarks>
/// A removal is paired with the nearest Time Shard, Echoes of Ruin, or Entropy's Claim cast within
/// <see cref="PairingMs"/> that no earlier window has claimed and that did not come before the window
/// opened. A cast with a completion is its
/// completion; an instant cast is its activation. A Time Shard's damage, healing, and Chrona
/// overcap are those inside <see cref="DamageWindowMs"/> after its completion; an Echoes of Ruin's enemies
/// are the applications inside <see cref="ApplicationWindowMs"/>.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(AeonaTalents.ContinuumShift)]
[Dependency<ChronaTracker>]
[Dependency<StaggerTracker>]
public sealed partial class ContinuumShiftAnalyzer : Analyzer, IContinuumShiftAnalyzer
{
    /// <summary>How far a removal may sit from the completion that spent it.</summary>
    public const int PairingMs = 2_500;

    /// <summary>How long after a Time Shard completion its damage, healing, and Chrona are credited to it.</summary>
    public const int DamageWindowMs = 1_500;

    /// <summary>How long after an Echoes of Ruin completion its applications are credited to it.</summary>
    public const int ApplicationWindowMs = 1_000;

    /// <summary>How long one Echoes of Ruin application runs. Codex <c>ability 1887</c>.</summary>
    public const int EchoesOfRuinDurationMs = 21_000;

    /// <summary>The tank's health share at or under which an Echoes of Ruin spend is justified.</summary>
    public const double TankDangerHealthFraction = 0.5;

    private readonly List<(int Start, int End)> _closed = [];
    private readonly List<Candidate> _candidates = [];
    private readonly List<DamageEvent> _timeShardDamage = [];
    private readonly List<HealEvent> _timeShardHeals = [];
    private readonly List<(int Timestamp, UnitKey Unit)> _echoesApplications = [];
    private readonly List<int> _fleetingHourCasts = [];

    private int? _openedAt;

    /// <summary>Every window in the pull, in the order they opened.</summary>
    public IReadOnlyList<ContinuumShiftWindow> Windows => field ??= Build();

    /// <summary>Windows the pull opened.</summary>
    public int Procs => Windows.Count;

    /// <summary>Windows that closed during the pull.</summary>
    public int ClosedWindows => Windows.Count(window => window.Spend != ContinuumShiftSpend.OpenAtPullEnd);

    /// <summary>Windows spent on Time Shard.</summary>
    public int SpentOnTimeShard => Windows.Count(window => window.Spend == ContinuumShiftSpend.TimeShard);

    /// <summary>Windows spent on Echoes of Ruin.</summary>
    public int SpentOnEchoesOfRuin => Windows.Count(window => window.Spend == ContinuumShiftSpend.EchoesOfRuin);

    /// <summary>Windows spent on Entropy's Claim.</summary>
    public int SpentOnEntropyClaim => Windows.Count(window => window.Spend == ContinuumShiftSpend.EntropyClaim);

    /// <summary>Windows removed with no cast beside them.</summary>
    public int Lost => Windows.Count(window => window.Spend == ContinuumShiftSpend.Lost);

    /// <summary>Closed windows whose spend was justified. Read it against <see cref="ClosedWindows"/>.</summary>
    public int JustifiedSpends => Windows.Count(window => window.Justified);

    /// <summary>Time Shard damage across every window spent on it.</summary>
    public long TimeShardDamage => Windows.Sum(window => window.Damage);

    /// <summary>Effective healing across every window spent on Time Shard.</summary>
    public long TimeShardEffectiveHealing => Windows.Sum(window => window.EffectiveHealing);

    /// <summary>Overheal across every window spent on Time Shard.</summary>
    public long TimeShardOverheal => Windows.Sum(window => window.Overheal);

    /// <summary>Chrona lost at the maximum across every window spent on Time Shard.</summary>
    public int ChronaOvercapped => Windows.Sum(window => window.ChronaOvercapped);

    /// <summary>Unfolding Doom casts in the pull.</summary>
    public int UnfoldingDoomCasts { get; private set; }

    /// <summary>Whether the player took Edge of Ruin.</summary>
    public bool EdgeOfRuinTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.EdgeOfRuin);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnApplied(ApplyBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnRefreshed(RefreshBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnRemoved(RemoveBuffEvent e)
    {
        if (_openedAt is not { } start) return;

        _closed.Add((start, e.Timestamp));
        _openedAt = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.TimeShard))]
    private void OnTimeShardCast(CastEvent e) => Consider(e, ContinuumShiftSpend.TimeShard);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuin))]
    private void OnEchoesOfRuinCast(CastEvent e) => Consider(e, ContinuumShiftSpend.EchoesOfRuin);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaim))]
    private void OnEntropyClaimCast(CastEvent e) => Consider(e, ContinuumShiftSpend.EntropyClaim);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoom))]
    private void OnUnfoldingDoomCast(CastEvent e)
    {
        if (e.Activation) return;

        UnfoldingDoomCasts++;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FleetingHour))]
    private void OnFleetingHourCast(CastEvent e) => _fleetingHourCasts.Add(e.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.TimeShard), nameof(Spells.TimeShardDamage)])]
    private void OnTimeShardDamage(DamageEvent e) => _timeShardDamage.Add(e);

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.TimeShard))]
    private void OnTimeShardHeal(HealEvent e) => _timeShardHeals.Add(e);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuinDot))]
    private void OnEchoesApplied(ApplyDebuffEvent e) => _echoesApplications.Add((e.Timestamp, AuraWindowLedger.KeyOf(e)));

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuinDot))]
    private void OnEchoesRefreshed(RefreshDebuffEvent e) => _echoesApplications.Add((e.Timestamp, AuraWindowLedger.KeyOf(e)));

    private void Consider(CastEvent e, ContinuumShiftSpend spend)
    {
        if (!e.Activation && _candidates.Count > 0 && _candidates[^1] is { Activation: true } activation
            && activation.Spend == spend && e.Timestamp - activation.Timestamp <= PairingMs)
            _candidates.RemoveAt(_candidates.Count - 1);

        _candidates.Add(new Candidate(e.Timestamp, spend, e.Activation));
    }

    private List<ContinuumShiftWindow> Build()
    {
        var windows = new List<ContinuumShiftWindow>(_closed.Count + 1);
        var claimed = new HashSet<int>();

        foreach (var (start, end) in _closed)
        {
            var candidate = Nearest(start, end, claimed);
            if (candidate is null)
            {
                windows.Add(new ContinuumShiftWindow(start, end, ContinuumShiftSpend.Lost, null, 0, 0, 0, 0, 0, null, false));
                continue;
            }

            claimed.Add(candidate.Timestamp);
            windows.Add(Describe(start, end, candidate));
        }

        if (_openedAt is { } open)
            windows.Add(new ContinuumShiftWindow(open, Pull.EndTime, ContinuumShiftSpend.OpenAtPullEnd, null, 0, 0, 0, 0, 0, null, false));

        windows.Sort((left, right) => left.Start.CompareTo(right.Start));
        return windows;
    }

    private Candidate? Nearest(int start, int removal, HashSet<int> claimed)
    {
        Candidate? nearest = null;
        foreach (var candidate in _candidates)
        {
            if (candidate.Timestamp < start || claimed.Contains(candidate.Timestamp)) continue;

            var distance = Math.Abs(candidate.Timestamp - removal);
            if (distance > PairingMs) continue;
            if (nearest is { } current && Math.Abs(current.Timestamp - removal) <= distance) continue;

            nearest = candidate;
        }

        return nearest;
    }

    private ContinuumShiftWindow Describe(int start, int end, Candidate candidate)
    {
        var cast = candidate.Timestamp;

        return candidate.Spend switch
        {
            ContinuumShiftSpend.TimeShard => new ContinuumShiftWindow(
                start,
                end,
                candidate.Spend,
                cast,
                _timeShardDamage.Where(hit => Inside(hit.Timestamp, cast, DamageWindowMs)).Sum(hit => hit.Amount),
                _timeShardHeals.Where(heal => Inside(heal.Timestamp, cast, DamageWindowMs)).Sum(heal => heal.Amount),
                _timeShardHeals.Where(heal => Inside(heal.Timestamp, cast, DamageWindowMs)).Sum(heal => heal.Overheal ?? 0),
                ChronaTracker.OvercapBetween(ResourceTypes.Primary, cast, cast + DamageWindowMs),
                0,
                null,
                false),
            ContinuumShiftSpend.EchoesOfRuin => new ContinuumShiftWindow(
                start,
                end,
                candidate.Spend,
                cast,
                0,
                0,
                0,
                0,
                _echoesApplications.Where(application => Inside(application.Timestamp, cast, ApplicationWindowMs)).Select(application => application.Unit).Distinct().Count(),
                TankHealthAt(cast),
                EdgeOfRuinTaken && _fleetingHourCasts.Any(fleetingHour => fleetingHour > cast && fleetingHour - cast <= EchoesOfRuinDurationMs)),
            _ => new ContinuumShiftWindow(start, end, candidate.Spend, cast, 0, 0, 0, 0, 0, null, false),
        };
    }

    private double? TankHealthAt(int timestamp)
    {
        if (StaggerTracker.TankId is not { } tank) return null;
        if (StaggerTracker.LatestBefore(tank, timestamp) is not { MaxHitPoints: > 0 } snapshot) return null;

        return (double)snapshot.HitPoints / snapshot.MaxHitPoints;
    }

    private static bool Inside(int timestamp, int cast, int windowMs) => timestamp >= cast && timestamp <= cast + windowMs;

    private sealed record Candidate(int Timestamp, ContinuumShiftSpend Spend, bool Activation);
}
