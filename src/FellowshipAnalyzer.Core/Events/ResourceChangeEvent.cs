using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Raised when a unit's resource pool changes, such as gaining orbs or generating mana.</summary>
[Fabricated]
public class ResourceChangeEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    /// <summary>The ability that caused the resource change.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Unique identifier for the unit whose resource changed.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the source is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>Unique identifier for the target of the ability that caused the resource change.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the target is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The id for the resource. See the <see cref="ResourceTypes"/> enum for all available resource types.
    /// </summary>
    public virtual ResourceTypes ResourceChangeType { get; set; }
    /// <summary>
    /// The amount of resource gained. This includes any wasted gain, see <see cref="Waste"/>
    /// </summary>
    public virtual double ResourceChange { get; set; }
    /// <summary>
    /// The amount of wasted resource gain (overcapped).
    /// </summary>
    public virtual double Waste { get; set; }
    /// <summary>The amount changed on a secondary resource pool affected by the same event, if any.</summary>
    public virtual double OtherResourceChange { get; set; } = 0;
}