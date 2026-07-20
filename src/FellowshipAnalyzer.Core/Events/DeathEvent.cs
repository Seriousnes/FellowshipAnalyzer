using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class DeathEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
{
    public virtual Ability? KillingAbility { get; set; }
    public virtual Ability Ability { get; set; } = new();
    public virtual FSLID AbilityGameId { get; set; }
    public virtual ICastTarget? Source { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}
