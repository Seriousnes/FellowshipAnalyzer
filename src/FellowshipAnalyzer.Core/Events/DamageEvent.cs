namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("damage")]
public record DamageEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IAmountEvent
{
    public virtual Unit? Actor { get; set; } = DefaultActors.Environment;
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int? SourceInstance { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual long Amount { get; set; }
    public virtual long? Absorbed { get; set; }
    public virtual long Mitigated { get; set; }
    public virtual long UnmitigatedAmount { get; set; }
}