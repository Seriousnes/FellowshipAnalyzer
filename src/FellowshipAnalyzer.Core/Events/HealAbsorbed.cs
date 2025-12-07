namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public record HealAbsorbed : HealEvent
{
    public override bool? Fabricated => true;
}
