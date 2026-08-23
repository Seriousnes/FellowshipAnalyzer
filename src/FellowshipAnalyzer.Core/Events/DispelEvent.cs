using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when a buff or debuff is removed from a unit by a dispel ability rather than expiring or being cancelled.</summary>
public class DispelEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>
    /// The ability used to dispel <see cref="ExtraAbility"/>
    /// </summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>
    /// The ability dispelled by <see cref="Ability"/>
    /// </summary>
    public virtual Ability? ExtraAbility { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="ExtraAbility"/>.</summary>
    public virtual FSLID ExtraAbilityGameId { get; set; }
    /// <summary>Whether the dispelled aura was a buff rather than a debuff.</summary>
    public virtual bool IsBuff { get; set; }
    /// <summary>Unique identifier for the unit that performed the dispel.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the unit that performed the dispel was friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>The instance index disambiguating <see cref="TargetId"/> when multiple copies of the target unit exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Unique identifier for the unit whose aura was dispelled.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the unit whose aura was dispelled was friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
}