namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public class FightEndEvent : Event
{
    public override bool? Fabricated => true;
    public override int DispatchOrder => EventDispatchOrder.FightEnd;
}
