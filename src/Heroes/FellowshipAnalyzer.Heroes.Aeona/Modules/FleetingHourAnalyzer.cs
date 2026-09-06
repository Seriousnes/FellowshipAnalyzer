using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>One Fleeting Hour cast, the window it opened, and the Surging Chrona it granted.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Window">The Fleeting Hour window this cast opened, or <see langword="null"/> when no window followed it.</param>
/// <param name="ActiveInPullMs">Milliseconds of the window that fell inside the pull the cast was made in.</param>
/// <param name="OutsideCombatMs">Milliseconds of the window that fell outside every pull.</param>
/// <param name="DelayMs">Combat milliseconds between Fleeting Hour becoming available and this cast.</param>
/// <param name="SurgingChronaGranted">The Chrona Surging Chrona granted for this cast, or <c>0</c> without the talent.</param>
/// <param name="SurgingChronaOvercapped">The share of <paramref name="SurgingChronaGranted"/> the cap refused.</param>
/// <param name="ActiveOnUnfoldingDoomCooldownMs">Milliseconds of the window with Unfolding Doom on cooldown.</param>
/// <param name="ActiveWithUnfoldingDoomAvailableMs">Milliseconds of the window with Unfolding Doom available.</param>
/// <param name="AvailableOnUnfoldingDoomCooldownMs">
/// Milliseconds before this cast with Fleeting Hour available and Unfolding Doom on cooldown.
/// </param>
public sealed record FleetingHourCast(
    int Timestamp,
    AuraWindow? Window,
    int ActiveInPullMs,
    int OutsideCombatMs,
    int DelayMs,
    int SurgingChronaGranted,
    int? SurgingChronaOvercapped,
    int ActiveOnUnfoldingDoomCooldownMs,
    int ActiveWithUnfoldingDoomAvailableMs,
    int AvailableOnUnfoldingDoomCooldownMs)
{
    /// <summary>Share of the window (0-1) with Unfolding Doom on cooldown.</summary>
    public double ActiveOnUnfoldingDoomCooldownShare =>
        Window is { Duration: > 0 } window ? (double)ActiveOnUnfoldingDoomCooldownMs / window.Duration : 0;
}

/// <summary>
/// Fleeting Hour across the dungeon: the windows it opened, how much of each ran with Unfolding Doom on
/// cooldown, how much of each fell inside a pull, how long the ability sat available before each cast, and
/// the Surging Chrona each cast granted.
/// </summary>
/// <remarks>
/// Registered dungeon-lifetime, because uptime outside combat is only answerable against the whole run:
/// the buff windows come from effect 2744 on the player, and the pull intervals in
/// <see cref="CombatLogParser.Pulls"/> divide them into combat and non-combat time. Unfolding Doom's
/// cooldown runs across pulls too, so its state comes from <see cref="SpellUsable"/> over the whole
/// dungeon rather than from the per-pull <c>UnfoldingDoomAnalyzer</c>.
/// </remarks>
[Dependency<ChronaTracker>]
[Dependency<SpellUsable>]
[After<ChronaTracker>]
public sealed partial class FleetingHourAnalyzer : Analyzer
{
    /// <summary>Milliseconds after a cast within which a Fleeting Hour window is credited to it.</summary>
    public const int WindowLinkToleranceMs = 500;

    private readonly List<AuraWindow> _windows = [];
    private readonly List<CastState> _casts = [];
    private readonly List<AvailabilityChange> _availability = [];
    private readonly List<AvailabilityChange> _unfoldingDoomAvailability = [];

    private int? _openedAt;

    /// <summary>
    /// Every Fleeting Hour window on the player, in the order they opened. A window still open when the
    /// dungeon ends closes at <see cref="CombatLogParser.DungeonEndTime"/>.
    /// </summary>
    public IReadOnlyList<AuraWindow> Windows =>
        _openedAt is { } start ? [.. _windows, CloseAtDungeonEnd(start)] : _windows;

    /// <summary>Every Fleeting Hour cast by the player, in cast order.</summary>
    public IReadOnlyList<FleetingHourCast> Casts => field ??= [.. _casts.Select(Build)];

    /// <summary>Time Fleeting Hour was active, across the whole report.</summary>
    public int TotalUptimeMs => Windows.Sum(window => window.Duration);

    /// <summary>Time Fleeting Hour was active with Unfolding Doom on cooldown.</summary>
    public int ActiveOnUnfoldingDoomCooldownMs => UnfoldingDoom.ActiveOnCooldownMs;

    /// <summary>Share of Fleeting Hour's active time (0-1) with Unfolding Doom on cooldown.</summary>
    public double ActiveOnUnfoldingDoomCooldownShare =>
        TotalUptimeMs <= 0 ? 0 : (double)ActiveOnUnfoldingDoomCooldownMs / TotalUptimeMs;

    /// <summary>Time Fleeting Hour was active with Unfolding Doom available.</summary>
    public int ActiveWithUnfoldingDoomAvailableMs => TotalUptimeMs - ActiveOnUnfoldingDoomCooldownMs;

    /// <summary>Share of Fleeting Hour's active time (0-1) with Unfolding Doom available.</summary>
    public double ActiveWithUnfoldingDoomAvailableShare =>
        TotalUptimeMs <= 0 ? 0 : (double)ActiveWithUnfoldingDoomAvailableMs / TotalUptimeMs;

    /// <summary>Time Unfolding Doom was on cooldown with Fleeting Hour available and not cast.</summary>
    public int AvailableOnUnfoldingDoomCooldownMs => UnfoldingDoom.AvailableOnCooldownMs;

    /// <summary>Time Fleeting Hour was active inside a pull.</summary>
    public int CombatUptimeMs => Windows.Sum(OverlapWithPulls);

    /// <summary>Time Fleeting Hour was active outside every pull.</summary>
    public int OutsideCombatMs => TotalUptimeMs - CombatUptimeMs;

    /// <summary>Share of Fleeting Hour's total active time (0-1) that fell outside every pull.</summary>
    public double OutsideCombatShare => TotalUptimeMs <= 0 ? 0 : (double)OutsideCombatMs / TotalUptimeMs;

    /// <summary>Combat time, summed over every pull.</summary>
    public int CombatMs => Owner.Pulls.Sum(pull => pull.Duration);

    /// <summary>Share of combat time Fleeting Hour was active, from 0 to 1.</summary>
    public double CombatUptime => CombatMs <= 0 ? 0 : (double)CombatUptimeMs / CombatMs;

    /// <summary>Combat time Fleeting Hour was off cooldown and not cast.</summary>
    public int AvailableInCombatMs => AvailableWindows.Sum(OverlapWithPulls);

    /// <summary>
    /// Mean combat milliseconds Fleeting Hour sat available before a cast. The delay counts combat time
    /// alone, so a recharge that completed between pulls contributes only the part of the wait that fell
    /// inside a pull.
    /// </summary>
    public double AverageDelayMs => _casts.Count == 0 ? 0 : Casts.Average(cast => cast.DelayMs);

    /// <summary>Whether the player took Surging Chrona.</summary>
    public bool SurgingChronaTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.SurgingChrona);

    /// <summary>The Chrona Surging Chrona grants on each Fleeting Hour cast.</summary>
    public int SurgingChronaGrant =>
        SurgingChronaTaken ? (int)Math.Round(Talents.SurgingChrona.ResourceGeneration?.Amount ?? 0) : 0;

    /// <summary>Chrona Surging Chrona granted, summed over every cast.</summary>
    public int SurgingChronaGranted => Casts.Sum(cast => cast.SurgingChronaGranted);

    /// <summary>Chrona the Surging Chrona grants lost at the cap, summed over every cast.</summary>
    public int SurgingChronaOvercapped => Casts.Sum(cast => cast.SurgingChronaOvercapped ?? 0);

    /// <summary>Share of the granted Chrona (0-1) the cap refused.</summary>
    public double SurgingChronaOvercapShare =>
        SurgingChronaGranted <= 0 ? 0 : (double)SurgingChronaOvercapped / SurgingChronaGranted;

    private List<AuraWindow> AvailableWindows => field ??= BuildWindows(_availability, available: true);

    private UnfoldingDoomState UnfoldingDoom => field ??= SummariseUnfoldingDoom();

    /// <summary>Whether a Fleeting Hour window covered <paramref name="timestamp"/>, endpoints included.</summary>
    /// <param name="timestamp">The instant to test.</param>
    public bool IsBuffActiveAt(int timestamp)
    {
        if (_openedAt is { } start && timestamp >= start && timestamp <= CloseAtDungeonEnd(start).End) return true;

        foreach (var window in _windows)
        {
            if (timestamp >= window.Start && timestamp <= window.End) return true;
        }

        return false;
    }

    /// <summary>Time Fleeting Hour was active inside <paramref name="pull"/>.</summary>
    /// <param name="pull">The pull to measure.</param>
    public int UptimeMsIn(PullStartEvent pull)
    {
        var total = 0;
        foreach (var window in Windows)
            total += Overlap(window, pull.StartTime, pull.EndTime);

        return total;
    }

    /// <summary>Share of <paramref name="pull"/> Fleeting Hour was active, from 0 to 1.</summary>
    /// <param name="pull">The pull to measure.</param>
    public double UptimeIn(PullStartEvent pull) =>
        pull.Duration <= 0 ? 0 : (double)UptimeMsIn(pull) / pull.Duration;

    /// <summary>The Fleeting Hour casts inside <paramref name="pull"/>, both bounds inclusive.</summary>
    /// <param name="pull">The pull to read.</param>
    public IReadOnlyList<FleetingHourCast> CastsIn(PullStartEvent pull) =>
        [.. Casts.Where(cast => cast.Timestamp >= pull.StartTime && cast.Timestamp <= pull.EndTime)];

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FleetingHourSelfBuff))]
    private void OnFleetingHourApplied(ApplyBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FleetingHourSelfBuff))]
    private void OnFleetingHourRefreshed(RefreshBuffEvent e) => _openedAt ??= e.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FleetingHourSelfBuff))]
    private void OnFleetingHourRemoved(RemoveBuffEvent e)
    {
        if (_openedAt is not { } start) return;

        _windows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _openedAt = null;
    }

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e)
    {
        _availability.Add(new AvailabilityChange(
            e.StartTime,
            SpellUsable.CooldownRemaining(Spells.FleetingHour.FSLID, e.StartTime) <= 0));

        _unfoldingDoomAvailability.Add(new AvailabilityChange(
            e.StartTime,
            SpellUsable.CooldownRemaining(Spells.UnfoldingDoom.FSLID, e.StartTime) <= 0));
    }

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.FleetingHour))]
    private void OnUsableChanged(UpdateSpellUsableEvent e) =>
        _availability.Add(new AvailabilityChange(e.Timestamp, e.IsAvailable));

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoom))]
    private void OnUnfoldingDoomUsableChanged(UpdateSpellUsableEvent e) =>
        _unfoldingDoomAvailability.Add(new AvailabilityChange(e.Timestamp, e.IsAvailable));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FleetingHour))]
    private void OnFleetingHourCast(CastEvent e)
    {
        var snapshot = ChronaSnapshot(e.SourceResources);

        _casts.Add(new CastState(
            e.Timestamp,
            snapshot?.Amount,
            snapshot is { Max: > 0 } ? snapshot.Max : ChronaTracker.MaxOf(ResourceTypes.Primary)));
    }

    private FleetingHourCast Build(CastState state)
    {
        var window = WindowOpenedBy(state.Timestamp);
        var granted = SurgingChronaGrant;
        var onCooldown = window is { } spanned ? Overlap(spanned, UnfoldingDoom.CooldownWindows) : 0;

        return new FleetingHourCast(
            state.Timestamp,
            window,
            window is { } opened ? OverlapWithPullAt(opened, state.Timestamp) : 0,
            window is { } active ? active.Duration - OverlapWithPulls(active) : 0,
            DelayFor(state.Timestamp),
            granted,
            state.ChronaBefore is { } before && state.ChronaCap > 0
                ? Math.Max(0, before + granted - state.ChronaCap)
                : null,
            onCooldown,
            window is { } whole ? whole.Duration - onCooldown : 0,
            AvailableOnUnfoldingDoomCooldownBefore(state.Timestamp));
    }

    private int AvailableOnUnfoldingDoomCooldownBefore(int castTimestamp)
    {
        foreach (var window in AvailableWindows)
        {
            if (castTimestamp < window.Start || castTimestamp > window.End) continue;

            return Overlap(new AuraWindow(window.Start, castTimestamp), UnfoldingDoom.CooldownWindows);
        }

        return 0;
    }

    private UnfoldingDoomState SummariseUnfoldingDoom()
    {
        var cooldowns = BuildWindows(_unfoldingDoomAvailability, available: false);
        var windows = Windows;
        var activeOnCooldown = 0;

        foreach (var window in windows)
            activeOnCooldown += Overlap(window, cooldowns);

        var availableOnCooldown = 0;

        foreach (var available in AvailableWindows)
        {
            foreach (var cooldown in cooldowns)
            {
                if (Intersect(available, cooldown) is not { } shared) continue;

                availableOnCooldown += shared.Duration - Overlap(shared, windows);
            }
        }

        return new UnfoldingDoomState(cooldowns, activeOnCooldown, availableOnCooldown);
    }

    private AuraWindow? WindowOpenedBy(int castTimestamp)
    {
        foreach (var window in Windows)
        {
            if (window.End < castTimestamp) continue;
            if (window.Start < castTimestamp) return window;

            return window.Start - castTimestamp <= WindowLinkToleranceMs ? window : null;
        }

        return null;
    }

    private int OverlapWithPullAt(AuraWindow window, int timestamp)
    {
        foreach (var pull in Owner.Pulls)
        {
            if (timestamp < pull.StartTime || timestamp > pull.EndTime) continue;

            return Overlap(window, pull.StartTime, pull.EndTime);
        }

        return 0;
    }

    private int DelayFor(int castTimestamp)
    {
        foreach (var window in AvailableWindows)
        {
            if (castTimestamp < window.Start || castTimestamp > window.End) continue;

            var delay = 0;
            foreach (var pull in Owner.Pulls)
                delay += Overlap(new AuraWindow(window.Start, castTimestamp), pull.StartTime, pull.EndTime);

            return delay;
        }

        return 0;
    }

    private List<AuraWindow> BuildWindows(List<AvailabilityChange> changes, bool available)
    {
        var windows = new List<AuraWindow>();
        var open = false;
        var openedAt = 0;

        foreach (var change in changes)
        {
            var matches = change.Available == available;
            if (matches == open) continue;

            open = matches;

            if (open) openedAt = change.Timestamp;
            else if (change.Timestamp > openedAt) windows.Add(new AuraWindow(openedAt, change.Timestamp));
        }

        if (open && Owner.DungeonEndTime > openedAt)
            windows.Add(new AuraWindow(openedAt, Owner.DungeonEndTime));

        return windows;
    }

    private int OverlapWithPulls(AuraWindow window)
    {
        var total = 0;
        foreach (var pull in Owner.Pulls)
            total += Overlap(window, pull.StartTime, pull.EndTime);

        return total;
    }

    private AuraWindow CloseAtDungeonEnd(int start) => new(start, Math.Max(start, Owner.DungeonEndTime));

    private static int Overlap(AuraWindow window, int start, int end)
    {
        var from = Math.Max(window.Start, start);
        var to = Math.Min(window.End, end);
        return to > from ? to - from : 0;
    }

    private static int Overlap(AuraWindow window, IReadOnlyList<AuraWindow> others)
    {
        var total = 0;
        foreach (var other in others)
            total += Overlap(window, other.Start, other.End);

        return total;
    }

    private static AuraWindow? Intersect(AuraWindow first, AuraWindow second)
    {
        var from = Math.Max(first.Start, second.Start);
        var to = Math.Min(first.End, second.End);
        return to > from ? new AuraWindow(from, to) : null;
    }

    private static ClassResource? ChronaSnapshot(ActorResources? resources)
    {
        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
        {
            if (resource.Type == ResourceTypes.Primary) return resource;
        }

        return null;
    }

    private readonly record struct AvailabilityChange(int Timestamp, bool Available);

    private sealed record UnfoldingDoomState(
        List<AuraWindow> CooldownWindows,
        int ActiveOnCooldownMs,
        int AvailableOnCooldownMs);

    private sealed record CastState(int Timestamp, int? ChronaBefore, int ChronaCap);
}
