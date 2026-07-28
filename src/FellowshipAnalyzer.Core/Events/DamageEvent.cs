using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>A single hit of damage dealt by an ability, attack, or environmental source.</summary>
public class DamageEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IAmountEvent
{
    /// <summary>The unit credited with the damage. Defaults to <see cref="DefaultActors.Environment"/> for environmental hits.</summary>
    public virtual Unit? Actor { get; set; } = DefaultActors.Environment;
    /// <summary>The ability that dealt the damage.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The instance index disambiguating <see cref="SourceId"/> when multiple copies of the source unit exist.</summary>
    public virtual int? SourceInstance { get; set; }
    /// <summary>Unique identifier for the unit that dealt the damage.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>The instance index disambiguating <see cref="TargetId"/> when multiple copies of the target unit exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Unique identifier for the unit that took the damage.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>The effective damage the target actually took, after absorbs and mitigation.</summary>
    public virtual long Amount { get; set; }
    /// <summary>The portion of the raw hit prevented by an absorb shield.</summary>
    public virtual long? Absorbed { get; set; }
    /// <summary>The type of hit (e.g., normal, critical, glancing).</summary>
    public virtual HitType HitType { get; set; }
    /// <summary>The portion of the hit prevented by damage reduction, separate from absorbs.</summary>
    public virtual long Mitigated { get; set; }
    /// <summary>The hit's damage before mitigation was applied.</summary>
    public virtual long UnmitigatedAmount { get; set; }
    /// <summary>Indicates whether the hit was a critical hit.</summary>
    public bool IsCritical => HitType is HitType.Crit or HitType.GreviousCrit;
}