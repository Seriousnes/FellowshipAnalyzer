namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public record AutoAttackCooldownEvent : Event, IAbilityEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual double Duration { get; set; }
    public virtual double Haste { get; set; }
    public virtual double AttackSpeed { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int TargetId { get; set; }
    public new ICooldownTriggerEvent Trigger { get; set; }
    public override bool? Fabricated => true;
}
