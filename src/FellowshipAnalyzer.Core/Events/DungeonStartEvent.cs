namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks the start of the whole dungeon, as opposed to the start of a single <see cref="PullStartEvent"/>.</summary>
[Fabricated]
public class DungeonStartEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
