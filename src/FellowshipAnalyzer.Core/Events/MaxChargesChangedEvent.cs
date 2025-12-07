namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public abstract record MaxChargesChangedEvent : Event
{
    /// <summary>
    /// The ID of the spell that's changing
    /// </summary>
    public int SpellId { get; set; }
    /// <summary>
    /// The number of charges we're increasing/decreasing by
    /// </summary>
    public int By { get; set; }
    public override bool? Fabricated => true;
}

public record MaxChargesIncreasedEvent : MaxChargesChangedEvent { }
public record MaxChargesDecreasedEvent : MaxChargesChangedEvent { }
