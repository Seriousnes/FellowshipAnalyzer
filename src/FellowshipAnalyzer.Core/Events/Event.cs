using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

public abstract record Event : IEventFilter
{
    /// <summary>
    /// Event type discriminator for <see cref="CustomJsonDerivedTypeAttribute"/>.
    /// </summary>
    [JsonPropertyName("type")]
    public virtual string EventType { get; set; }
    /// <summary>
    /// Timestamp in milliseconds
    /// </summary>
    public virtual int Timestamp { get; set; }
    /// <summary>
    /// Fight ID
    /// </summary>
    public virtual int Fight { get; set; }
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
    /// Was the event created by WoWA
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
    /// A WoWA analyzer has reordered this event
    /// </summary>
    public virtual bool? Reordered { get; set; }
}
