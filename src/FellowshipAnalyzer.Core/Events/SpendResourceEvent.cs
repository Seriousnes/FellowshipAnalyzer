namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public record SpendResourceEvent : Event, IAbilityEvent
{
    public int SourceId { get; set; }
    public int ResourceChange { get; set; }
    public int ResourceChangeType { get; set; }
    public Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public override bool? Fabricated => true;
}
