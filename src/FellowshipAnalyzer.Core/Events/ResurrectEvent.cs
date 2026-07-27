using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when a fallen unit is brought back to life.</summary>
public class ResurrectEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    /// <summary>The ability that performed the resurrection.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Unique identifier for the unit that cast the resurrection.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the source is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>Unique identifier for the unit that was resurrected.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the target is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
}
