using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public class ResourceChangeEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The id for the resource. See the <see cref="Resource"/> file for all available resource types.
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
    public virtual double OtherResourceChange { get; set; } = 0;
}