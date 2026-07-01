using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public class GlobalCooldownEvent : Event, IAbilityEvent
{
    public virtual Ability Ability { get; set; }
    public virtual FSLID AbilityGameId { get; set; }
    public virtual int Duration { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual ICooldownTriggerEvent MyProperty { get; set; }
    public override bool? Fabricated => true;
}
