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
    /// The event happened before the pull. Added by WoWA
    /// </summary>
    public virtual bool? Prepull { get; set; }
    /// <summary>
    /// Other events associated with this event. Added by WoWA normalizers
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
    public virtual object? Trigger { get; set; }
    /// <summary>
    /// Event content modified by WoWA
    /// </summary>
    public virtual bool? Modified { get; set; }
    /// <summary>
    /// A WoWA analyzer has reordered this event
    /// </summary>
    public virtual bool? Reordered { get; set; }
}
