namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks the start of the whole fight (report), as opposed to the start of a single <see cref="Analysis.Pull"/>.</summary>
[Fabricated]
public class DungeonStartEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
