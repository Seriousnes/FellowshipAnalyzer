using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Raised when a unit's resource pool changes, such as gaining orbs, spending mana or clearing Stagger.
/// <para>
/// <see cref="Analysis.Normalizers.ResourceChangeNormalizer"/> fabricates one for every change it reads
/// in the resource blocks on events, and places it immediately before the event whose block revealed the
/// change, which <see cref="Event.Trigger"/> names.
/// </para>
/// </summary>
[Fabricated]
public class ResourceChangeEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    /// <summary>
    /// The ability that caused the resource change: the ability of the event whose block revealed it, or
    /// <see cref="Ability.UnknownAbility"/> when that event is a cast or names no ability. A cast's block
    /// is read before the cast resolves, so a change it reveals happened before the cast.
    /// </summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Unique identifier for the unit whose resource changed.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the source is friendly.</summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>
    /// Unique identifier for the target of the ability that caused the resource change, or the unit whose
    /// resource changed when the revealing event names no target.
    /// </summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the target is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The id for the resource. See the <see cref="ResourceTypes"/> enum for all available resource types.
    /// </summary>
    public virtual ResourceTypes ResourceChangeType { get; set; }
    /// <summary>
    /// The amount of resource gained, negative for a loss. This includes any wasted gain, see <see cref="Waste"/>.
    /// The first reading of a unit's resource is a change from zero, so it can be zero itself.
    /// </summary>
    public virtual double ResourceChange { get; set; }
    /// <summary>
    /// The amount of wasted resource gain (overcapped).
    /// </summary>
    public virtual double Waste { get; set; }
    /// <summary>The amount changed on a secondary resource pool affected by the same event, if any.</summary>
    public virtual double OtherResourceChange { get; set; } = 0;
    /// <summary>
    /// The unit's amount of the resource after the change, or <see langword="null"/> when the event declares
    /// only <see cref="ResourceChange"/>.
    /// </summary>
    public virtual int? ResourceAmount { get; set; }
    /// <summary>
    /// The unit's amount of the resource before the change, or <see langword="null"/> when this is the first
    /// reading of it or the event declares only <see cref="ResourceChange"/>.
    /// </summary>
    public virtual int? PreviousResourceAmount { get; set; }
    /// <summary>
    /// The resource's maximum at the change, or <see langword="null"/> when the event declares none. <c>-1</c>
    /// is the no-maximum sentinel.
    /// </summary>
    public virtual int? ResourceMax { get; set; }
    /// <summary>
    /// The resource block of the unit whose resource changed, as the revealing event reported it, for the
    /// unit's hit points at the change. <see langword="null"/> when the event declares no block.
    /// </summary>
    public virtual ActorResources? UnitResources { get; set; }
}
