using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when a unit dies, naming the ability that landed the killing blow.</summary>
public class DeathEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
{
    /// <summary>The ability whose hit killed the target.</summary>
    public virtual Ability? KillingAbility { get; set; }
    /// <summary>The ability associated with this death event.</summary>
    public virtual Ability Ability { get; set; } = new();
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The unit that killed the target.</summary>
    public virtual ICastTarget? Source { get; set; }
    /// <summary>Whether the unit that landed the killing blow was friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>The instance index disambiguating <see cref="TargetId"/> when multiple copies of the target unit exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Unique identifier for the unit that died.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the unit that died was friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
}
