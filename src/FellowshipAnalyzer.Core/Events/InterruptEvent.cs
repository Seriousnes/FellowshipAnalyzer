using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Records a player interrupting a target's ability cast.</summary>
public class InterruptEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>The ability used to interrupt the target's cast.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The ability whose cast was interrupted.</summary>
    public virtual Ability ExtraAbility { get; set; }
    /// <summary>The FSLID identifying <see cref="ExtraAbility"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID ExtraAbilityGameId { get; set; }
    /// <summary>Unique identifier for the actor who performed the interrupt.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the actor who performed the interrupt is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>Disambiguates <see cref="TargetId"/> when multiple copies of the interrupted target exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Unique identifier for the actor whose cast was interrupted.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the actor whose cast was interrupted is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
}
