namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("death")]
public record DeathEvent : Event, IAbilityEvent, IHasTargetEvent
{
    public virtual Ability? KillingAbility { get; set; }
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual ICastTarget Source { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}
