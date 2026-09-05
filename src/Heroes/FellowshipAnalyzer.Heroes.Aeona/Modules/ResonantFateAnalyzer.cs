using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Resonant Fate.</summary>
public interface IResonantFateAnalyzer : IAnalyzerSurface;

/// <summary>One stretch at Resonant Fate's maximum stacks.</summary>
/// <param name="ReachedAt">When the counter reached its maximum.</param>
/// <param name="SpentAt">When the hold ended, or the pull's end time.</param>
/// <param name="GrantedTo">The unit the damage reduction applied to.</param>
/// <param name="GrantedToTank">Whether <paramref name="GrantedTo"/> is the tank.</param>
/// <param name="DamageReductionActiveMs">Milliseconds the damage reduction on the granted unit was active.</param>
public sealed record ResonantFateHold(
    int ReachedAt,
    int SpentAt,
    int? GrantedTo,
    bool GrantedToTank,
    int DamageReductionActiveMs)
{
    /// <summary>Milliseconds the counter was at its maximum before this hold ended.</summary>
    public int HeldAtMaximumMs => SpentAt - ReachedAt;
}

/// <summary>Resonant Fate over one pull.</summary>
/// <remarks>
/// A hold ends on the first signal that it was spent: the damage reduction applied, Resonant Fate
/// Exhausted applied to the player, or the counter falling back below its maximum.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(AeonaTalents.ResonantFate)]
[Dependency<StaggerTracker>]
public sealed partial class ResonantFateAnalyzer : Analyzer, IResonantFateAnalyzer
{
    /// <summary>The stacks Resonant Fate's counter holds at most.</summary>
    public const int MaximumStacks = 100;

    private readonly List<HoldState> _holds = [];
    private readonly Dictionary<int, List<AuraWindow>> _damageReduction = [];
    private readonly Dictionary<int, int> _openDamageReduction = [];

    private HoldState? _open;

    /// <summary>Every stretch the counter was at its maximum during the pull, in order.</summary>
    public IReadOnlyList<ResonantFateHold> Holds => field ??=
        [.. (_open is { } open ? _holds.Append(open) : _holds).Select(Build)];

    /// <summary>Milliseconds of the pull the counter was at its maximum.</summary>
    public int HeldAtMaximumMs => Holds.Sum(hold => hold.HeldAtMaximumMs);

    /// <summary>Holds granted.</summary>
    public int HoldsGranted => Holds.Count(hold => hold.GrantedTo is not null);

    /// <summary>Every stretch the tank had the damage reduction active.</summary>
    public IReadOnlyList<AuraWindow> DamageReductionWindows => field ??= TankWindows();

    /// <summary>Milliseconds of the pull the tank had the damage reduction active.</summary>
    public int DamageReductionActiveMs => DamageReductionWindows.Sum(window => window.Duration);

    /// <summary>Share of the pull (0-1) the tank had the damage reduction active.</summary>
    public double DamageReductionUptime =>
        Pull.Duration > 0 ? Math.Min(1d, DamageReductionActiveMs / (double)Pull.Duration) : 0;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ResonantFate))]
    private void OnCounterStacked(ApplyBuffStackEvent e)
    {
        if (e.Stack >= MaximumStacks) Open(e.Timestamp);
        else Close(e.Timestamp);
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ResonantFate))]
    private void OnCounterStackRemoved(RemoveBuffStackEvent e)
    {
        if (e.Stack < MaximumStacks) Close(e.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ResonantFate))]
    private void OnCounterRemoved(RemoveBuffEvent e) => Close(e.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ResonantFateExhausted))]
    private void OnExhausted(ApplyBuffEvent e) => Close(e.Timestamp);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.ResonantFateDamageReduction))]
    private void OnDamageReductionApplied(ApplyBuffEvent e)
    {
        Close(e.Timestamp, e.TargetId);
        OpenDamageReduction(e.TargetId, e.Timestamp);
    }

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.ResonantFateDamageReduction))]
    private void OnDamageReductionRefreshed(RefreshBuffEvent e) => OpenDamageReduction(e.TargetId, e.Timestamp);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.ResonantFateDamageReduction))]
    private void OnDamageReductionRemoved(RemoveBuffEvent e)
    {
        if (!_openDamageReduction.Remove(e.TargetId, out var start)) return;

        WindowsFor(e.TargetId).Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
    }

    private void Open(int timestamp) => _open ??= new HoldState(timestamp);

    private void Close(int timestamp, int? grantedTo = null)
    {
        if (_open is not { } state) return;

        state.SpentAt = timestamp;
        state.GrantedTo = grantedTo;
        _holds.Add(state);
        _open = null;
    }

    private void OpenDamageReduction(int unitId, int timestamp) =>
        _openDamageReduction.TryAdd(unitId, timestamp);

    private List<AuraWindow> WindowsFor(int unitId)
    {
        if (!_damageReduction.TryGetValue(unitId, out var windows))
            _damageReduction[unitId] = windows = [];

        return windows;
    }

    private ResonantFateHold Build(HoldState state)
    {
        var grantedTo = state.GrantedTo;

        return new ResonantFateHold(
            state.ReachedAt,
            state.SpentAt ?? Pull.EndTime,
            grantedTo,
            grantedTo is { } unit && StaggerTracker.TankIds.Contains(unit),
            grantedTo is { } target ? ActiveMsFor(target, state.SpentAt ?? Pull.EndTime) : 0);
    }

    private int ActiveMsFor(int unitId, int from)
    {
        var total = 0;
        foreach (var window in ClosedWindowsFor(unitId))
        {
            if (window.End < from) continue;
            total += window.Duration;
        }

        return total;
    }

    private List<AuraWindow> ClosedWindowsFor(int unitId)
    {
        var windows = new List<AuraWindow>(WindowsFor(unitId));
        if (_openDamageReduction.TryGetValue(unitId, out var start))
            windows.Add(new AuraWindow(start, Math.Max(start, Pull.EndTime)));

        return windows;
    }

    private List<AuraWindow> TankWindows() =>
        StaggerTracker.TankId is { } tank ? ClosedWindowsFor(tank) : [];

    private sealed class HoldState(int reachedAt)
    {
        public int ReachedAt { get; } = reachedAt;
        public int? SpentAt { get; set; }
        public int? GrantedTo { get; set; }
    }
}
