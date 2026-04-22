namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("absorbed")]
public record AbsorbedEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetEvent, IAmountEvent
{
    public virtual int SourceId { get; set; }
    public virtual int TargetId { get; set; }
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int? AttackerId { get; set; }
    public virtual int? AttackerInstance { get; set; }
    public virtual long Amount { get; set; }
    public virtual Ability ExtraAbility { get; set; }
    public virtual int ExtraAbilityGameId { get; set; }
}