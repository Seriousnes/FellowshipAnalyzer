namespace FellowshipAnalyzer.Core.Events;

public class ResurrectEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}
