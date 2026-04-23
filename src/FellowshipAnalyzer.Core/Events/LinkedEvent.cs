namespace FellowshipAnalyzer.Core.Events;

public class LinkedEvent(Event @event, string relation)
{
    /// <summary>
    /// A string specifying the relationship of the linked event. Used as key during lookup
    /// </summary>
    public string Relation { get; init; } = relation;
    /// <summary>
    /// The linked event
    /// </summary>
    public Event Event { get; init; } = @event;
}