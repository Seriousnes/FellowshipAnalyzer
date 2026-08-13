using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Base type for every combat log event deserialized from the FellowshipLogs GraphQL API.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public abstract partial class Event : IEventFilter
{
    /// <summary>
    /// Timestamp in milliseconds
    /// </summary>
    public virtual int Timestamp { get; set; }
    /// <summary>
    /// The id of the dungeon this event belongs to. Bound to the raw <c>fight</c> field, which is
    /// what Fellowship Logs calls a dungeon.
    /// </summary>
    [JsonPropertyName("fight")]
    public virtual int DungeonId { get; set; }
    /// <summary>
    /// Resource snapshot for the source actor (typically the casting player).
    /// </summary>
    public virtual ActorResources? SourceResources { get; set; }
    /// <summary>
    /// Resource snapshot for the target actor.
    /// </summary>
    public virtual ActorResources? TargetResources { get; set; }
    /// <summary>
    /// The event happened before the pull
    /// </summary>
    public virtual bool? Prepull { get; set; }
    /// <summary>
    /// Other events associated with this event
    /// </summary>
    [JsonIgnore]
    public virtual List<LinkedEvent> LinkedEvents { get; set; } = [];
    /// <summary>
    /// Set of <see cref="EventLink"/> that have been processed already to avoid duplicated linking.
    /// </summary>
    public virtual HashSet<EventLink> ProcessedLinks { get; set; } = [];
    /// <summary>
    /// Was the event created by FSA
    /// </summary>
    public virtual bool? Fabricated { get; set; }
    /// <summary>
    /// If this event was triggered, this is the triggering event
    /// </summary>
    public virtual Event? Trigger { get; set; }
    /// <summary>
    /// Event content modified by FSA
    /// </summary>
    public virtual bool? Modified { get; set; }
    /// <summary>
    /// An analyzer has reordered this event.
    /// </summary>
    public virtual bool? Reordered { get; set; }
}
