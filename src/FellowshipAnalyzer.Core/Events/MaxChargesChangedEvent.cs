namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public abstract class MaxChargesChangedEvent : Event
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

public class MaxChargesIncreasedEvent : MaxChargesChangedEvent { }
public class MaxChargesDecreasedEvent : MaxChargesChangedEvent { }
