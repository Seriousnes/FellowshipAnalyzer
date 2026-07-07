namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Secondary sort keys applied to <see cref="Event.DispatchOrder"/> so boundary events order
/// deterministically against each other and against gameplay events that share a timestamp
/// (the primary sort is by <see cref="Event.Timestamp"/> alone and is not stable).
///
/// Ordering ascending: <see cref="FightStart"/> &lt; <see cref="PullStart"/> &lt;
/// <see cref="Default"/> (gameplay) &lt; <see cref="PullEnd"/> &lt; <see cref="FightEnd"/>.
/// Opens sort before same-timestamp gameplay events and closes sort after them, so a pull's
/// boundaries nest inside the fight's and an event at a pull's end timestamp is processed inside
/// that pull (closed interval). The sign of <see cref="PullEnd"/> relative to <see cref="Default"/>
/// is the single switch that selects closed- vs half-open-interval semantics for pull windows.
/// </summary>
public static class EventDispatchOrder
{
    public const int FightStart = -2;
    public const int PullStart = -1;
    public const int Default = 0;
    public const int PullEnd = 1;
    public const int FightEnd = 2;
}
