using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models Chronoshift's Cooldown Recovery effect. While the player channels Chronoshift, every
/// cooldown recovers 9× faster (800% increased recovery). The channel lasts a fixed 3 seconds
/// (haste-independent) unless an <see cref="EndChannelEvent"/> cancels it early. The recovery
/// modifier is set on <see cref="SpellUsable"/> for the channel window and cleared when it ends;
/// because it is <i>set</i> rather than stacked, an unmatched begin/end (Fellowship logs the
/// channel end for only a minority of channels) cannot compound the modifier.
/// </summary>
[ActiveWhen<HasChronoshiftGear>]
public sealed partial class ChronoshiftAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    /// <summary>800% increased cooldown recovery = 9× total recovery rate.</summary>
    private const double RecoveryRate = 9.0;

    /// <summary>Chronoshift channels a fixed 3 seconds unless an EndChannel cancels it early.</summary>
    private const int ChannelDurationMs = 3000;

    private readonly List<ChronoshiftWindow> _windows = [];
    private int? _scheduledEnd;

    /// <summary>All Chronoshift recovery windows observed for the selected player.</summary>
    public IReadOnlyList<ChronoshiftWindow> Windows => _windows;

    [On<BeginChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnBeginChannel(BeginChannelEvent e)
    {
        CloseWindow(e.Timestamp);
        _spellUsable.SetCooldownRecoveryRate(RecoveryRate, e.Timestamp);
        _scheduledEnd = e.Timestamp + ChannelDurationMs;
        _windows.Add(new ChronoshiftWindow(e.Timestamp, _scheduledEnd.Value));
    }

    [On<EndChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnEndChannel(EndChannelEvent e) => CloseWindow(e.Timestamp);

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        if (_scheduledEnd is int end && e.Timestamp >= end)
            CloseWindow(end);
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent e) => CloseWindow(e.Timestamp);

    /// <summary>
    /// Closes the active recovery window at the earlier of <paramref name="timestamp"/> and the
    /// scheduled 3-second end, clearing the recovery modifier. A no-op when no window is open, so
    /// a stray or duplicate close cannot drive the recovery rate below 1.
    /// </summary>
    private void CloseWindow(int timestamp)
    {
        if (_scheduledEnd is not int scheduledEnd) return;
        var closeAt = Math.Min(timestamp, scheduledEnd);
        _spellUsable.SetCooldownRecoveryRate(1.0, closeAt);
        _scheduledEnd = null;
        _windows[^1] = _windows[^1] with { EndTimestamp = closeAt };
    }
}

/// <summary>A single Chronoshift channel window: the interval over which cooldown recovery was boosted.</summary>
public record ChronoshiftWindow(int BeginTimestamp, int EndTimestamp);
