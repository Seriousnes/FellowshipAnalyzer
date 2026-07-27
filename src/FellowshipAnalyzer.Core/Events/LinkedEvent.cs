namespace FellowshipAnalyzer.Core.Events;

/// <summary>Links one <see cref="Event"/> to another under a named relationship, stored in <see cref="Event.LinkedEvents"/> for lookup by <see cref="Relation"/>.</summary>
/// <param name="event">The linked event.</param>
/// <param name="relation">A string specifying the relationship of the linked event. Used as key during lookup.</param>
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