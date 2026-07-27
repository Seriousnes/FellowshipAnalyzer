namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks the end of the whole fight (report), as opposed to the end of a single <see cref="Analysis.Pull"/>.</summary>
[Fabricated]
public class FightEndEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
