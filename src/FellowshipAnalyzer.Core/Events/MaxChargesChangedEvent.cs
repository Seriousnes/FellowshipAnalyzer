namespace FellowshipAnalyzer.Core.Events;

/// <summary>A fabricated event recording a change to a spell's maximum charge count. See <see cref="MaxChargesIncreasedEvent"/> and <see cref="MaxChargesDecreasedEvent"/>.</summary>
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
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}

/// <summary>A spell's maximum charge count increased by <see cref="MaxChargesChangedEvent.By"/>.</summary>
public class MaxChargesIncreasedEvent : MaxChargesChangedEvent { }
/// <summary>A spell's maximum charge count decreased by <see cref="MaxChargesChangedEvent.By"/>.</summary>
public class MaxChargesDecreasedEvent : MaxChargesChangedEvent { }
