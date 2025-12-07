namespace FellowshipAnalyzer.Core.Events;

[WCLEventDiscriminator("absorbed")]
public record AbsorbedEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetEvent, IHitpointsEvent, IAmountEvent
{
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual long HitPoints { get; set; }
    public virtual long MaxHitPoints { get; set; }
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int? AttackerId { get; set; }
    public virtual bool? AttackerIsFriendly { get; set; }
    public virtual int? AttackerInstance { get; set; }
    public virtual long Amount { get; set; }
    public virtual Ability ExtraAbility { get; set; }
    public virtual int ExtraAbilityGameId { get; set; }
}