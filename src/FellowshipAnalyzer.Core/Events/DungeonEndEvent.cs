namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks the end of the whole dungeon, as opposed to the end of a single <see cref="PullStartEvent"/>.</summary>
[Fabricated]
public class DungeonEndEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
