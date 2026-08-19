using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Projects the selected player's total effective haste from <see cref="StatTracker"/> and fabricates a
/// <see cref="ChangeHasteEvent"/> whenever it moves, so consumers that only care about haste do not have to
/// re-derive it from every stat change. <see cref="StatTracker"/> owns both haste channels: the rating and
/// the flat percentages that add to it.
/// </summary>
/// <remarks>
/// Haste time-scaling: <c>effective_duration = base_duration × 100 / (100 + hastePercent)</c>
/// where <c>hastePercent = Current × 100</c> (e.g., 30 for 30% haste).
/// </remarks>
[After<StatTracker>]
public sealed partial class Haste(Lazy<StatTracker> statTracker) : Analyzer
{
    private double _lastNotified;

    /// <summary>
    /// Current total effective haste as a decimal fraction (0.30 = 30%): the rating converted through
    /// diminishing returns plus every active flat percentage.
    /// </summary>
    public double Current => _statTracker.CurrentHastePercentage;

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e) => Notify(e);

    [On<ChangeStatsEvent>(To = Actor.Player)]
    private void OnChangeStats(ChangeStatsEvent e)
    {
        if (e is ChangeHasteEvent) return;
        Notify(e);
    }

    /// <summary>
    /// Scales a base duration by the current haste percentage.
    /// <c>effective = baseDurationMs × 100 / (100 + hastePercent)</c>.
    /// </summary>
    public int ScaleDuration(int baseDurationMs) =>
        (int)(baseDurationMs * 100.0 / (100.0 + Current * 100.0));

    private void Notify(Event trigger)
    {
        var newHaste = Current;
        if (double.IsNaN(newHaste) || double.IsInfinity(newHaste)) return;

        var oldHaste = _lastNotified;
        if (oldHaste == newHaste) return;

        _lastNotified = newHaste;

        var stats = trigger as ChangeStatsEvent;

        Owner.EventEmitter.FabricateEvent(new ChangeHasteEvent
        {
            Timestamp = trigger.Timestamp,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            OldHaste = oldHaste,
            NewHaste = newHaste,
            Before = stats?.Before ?? new Stats(),
            After = stats?.After ?? new Stats(),
            Delta = stats?.Delta ?? new Stats(),
        }, trigger);
    }
}
