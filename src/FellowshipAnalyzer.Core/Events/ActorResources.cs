namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// An actor's resources (source or target) attached to a log event.
/// Corresponds to the <c>sourceResources</c> or <c>targetResources</c> JSON fields.
/// </summary>
public class ActorResources
{
    /// <summary>The actor's current hit points at the time of the event.</summary>
    public long HitPoints { get; set; }

    /// <summary>The actor's maximum hit points.</summary>
    public long MaxHitPoints { get; set; }

    /// <summary>The actor's current absorb shield amount.</summary>
    public int Absorb { get; set; }

    /// <summary>The actor's X coordinate on the dungeon map at the time of the event.</summary>
    public double X { get; set; }

    /// <summary>The actor's Y coordinate on the dungeon map at the time of the event.</summary>
    public double Y { get; set; }

    /// <summary>The direction the actor was facing, in the game's angular units.</summary>
    public double Facing { get; set; }

    /// <summary>
    /// The resources held by this actor at the time of the event.
    /// An array of resource entries, one per tracked resource type.
    /// </summary>
    public List<ClassResource> Resources { get; set; } = [];
}
