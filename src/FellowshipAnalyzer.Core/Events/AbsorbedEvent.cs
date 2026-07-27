using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when an absorption shield consumes some or all of an incoming hit that would otherwise have applied as damage or healing.</summary>
public class AbsorbedEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetEvent, IAmountEvent
{
    /// <summary>Id of the unit whose absorption shield consumed the hit.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Id of the unit that was hit and had the hit reduced by the absorption.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>The absorption ability whose shield consumed the hit described by <see cref="ExtraAbility"/>.</summary>
    public virtual Ability Ability { get; set; } = null!;
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Id of the unit whose attack was absorbed, when known.</summary>
    public virtual int? AttackerId { get; set; }
    /// <summary>Disambiguates <see cref="AttackerId"/> when multiple copies of the same attacker exist.</summary>
    public virtual int? AttackerInstance { get; set; }
    /// <summary>The amount of damage or healing consumed by the absorption shield.</summary>
    public virtual long Amount { get; set; }
    /// <summary>The ability that dealt the hit absorbed by <see cref="Ability"/>.</summary>
    public virtual Ability? ExtraAbility { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="ExtraAbility"/>.</summary>
    public virtual FSLID ExtraAbilityGameId { get; set; }
}