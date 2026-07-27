using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when a player summons a pet or other temporary companion unit.</summary>
public class SummonEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>The ability that performed the summon.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Unique identifier for the unit that cast the summon.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the summoning unit was friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>The summoned pet unit, including its owner and fight windows.</summary>
    public virtual PetInfo Target { get; set; }
    /// <summary>Disambiguates <see cref="TargetId"/> when multiple copies of the summoned unit exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Unique identifier for the summoned unit.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the summoned unit was friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
}
