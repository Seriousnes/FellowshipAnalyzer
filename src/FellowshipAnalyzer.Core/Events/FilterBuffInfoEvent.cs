namespace FellowshipAnalyzer.Core.Events;

/// <summary>A buff event that FellowshipLogs delivers only to satisfy a query filter, rather than describing a real state change.</summary>
public class FilterBuffInfoEvent : BuffEvent
{
}
