namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Resource snapshot for an actor (source or target) attached to a log event.
/// Corresponds to the <c>sourceResources</c> or <c>targetResources</c> JSON fields.
/// </summary>
public class ActorResources
{
    public long HitPoints { get; set; }
    public long MaxHitPoints { get; set; }
    public int Absorb { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Facing { get; set; }

    /// <summary>
    /// The resources held by this actor at the time of the event.
    /// An array of resource entries, one per tracked resource type.
    /// </summary>
    public List<ClassResource> Resources { get; set; } = [];
}
