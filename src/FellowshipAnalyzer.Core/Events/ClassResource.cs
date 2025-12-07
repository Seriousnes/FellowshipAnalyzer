using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// The state of a resource. Events typically have an array of these objects, one per resource.
/// </summary>
public class ClassResource
{
    /// <summary>
    /// The amount of the resource. Can be before or after the event depending on event type.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// The maximum amount of the resource.
    /// </summary>
    public int Max { get; set; }

    /// <summary>
    /// The type of resource.
    /// </summary>
    public ResourceTypes Type { get; set; }

    /// <summary>
    /// The amount of resource spent (only populated for spenders).
    /// </summary>
    public int? Cost { get; set; }
}
