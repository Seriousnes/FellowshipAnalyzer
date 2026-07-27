using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Reports bonus attacks granted to the source by <see cref="Ability"/>, such as a proc that grants extra swings.</summary>
public class ExtraAttacksEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    /// <summary>The ability that granted the extra attacks.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Unique identifier for the source. Nobody else will have this ID.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the source is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>The raid marker icon assigned to the source, if any.</summary>
    public virtual int? SourceMarker { get; set; }
    /// <summary>Unique identifier for the target. Nobody else will have this ID.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the target is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>The raid marker icon assigned to the target, if any.</summary>
    public virtual int? TargetMarker { get; set; }
    /// <summary>The number of extra attacks granted.</summary>
    public virtual int ExtraAttacks { get; set; }
}
