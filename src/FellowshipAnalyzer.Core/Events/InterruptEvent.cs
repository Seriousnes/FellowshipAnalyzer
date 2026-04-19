namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("interrupt")]
public record InterruptEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual Ability ExtraAbility { get; set; }
    public virtual int ExtraAbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}
