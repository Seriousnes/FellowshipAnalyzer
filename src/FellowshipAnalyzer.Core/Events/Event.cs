using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Base type for every combat log event deserialized from the FellowshipLogs GraphQL API.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public abstract partial class Event : IEventFilter
{
    private List<LinkedEvent>? _linkedEvents;

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
    /// Other events associated with this event, established by an
    /// <see cref="Analysis.Normalizers.EventLinkNormalizer"/> and read back by
    /// <see cref="RelatedEvents{TEvent}"/>. Empty until a normalizer links something. The list is this
    /// event's own, created on first access, so adding to it links the event.
    /// </summary>
    [JsonIgnore]
    public List<LinkedEvent> LinkedEvents => _linkedEvents ??= [];
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

    /// <summary>
    /// Associates <paramref name="event"/> with this event under <paramref name="relation"/>. Adding a
    /// pair that is already linked does nothing, so normalizing one event stream more than once cannot
    /// double a link.
    /// </summary>
    /// <param name="relation">The relationship name the link is read back by.</param>
    /// <param name="event">The event to associate with this one.</param>
    public void AddRelatedEvent(string relation, Event @event)
    {
        _linkedEvents ??= [];

        foreach (var link in _linkedEvents)
        {
            if (link.Relation == relation && ReferenceEquals(link.Event, @event)) return;
        }

        _linkedEvents.Add(new LinkedEvent(@event, relation));
    }

    /// <summary>Every <typeparamref name="TEvent"/> linked to this event under <paramref name="relation"/>, in the order the links were established.</summary>
    /// <typeparam name="TEvent">The event type to return; a link to any other type is skipped.</typeparam>
    /// <param name="relation">The relationship name the links were established under.</param>
    public IEnumerable<TEvent> RelatedEvents<TEvent>(string relation) where TEvent : Event
    {
        if (_linkedEvents is null) yield break;

        foreach (var link in _linkedEvents)
        {
            if (link.Relation == relation && link.Event is TEvent typed) yield return typed;
        }
    }

    /// <summary>The first <typeparamref name="TEvent"/> linked to this event under <paramref name="relation"/>, or <c>null</c> when there is none.</summary>
    /// <typeparam name="TEvent">The event type to return; a link to any other type is skipped.</typeparam>
    /// <param name="relation">The relationship name the link was established under.</param>
    public TEvent? RelatedEvent<TEvent>(string relation) where TEvent : Event =>
        RelatedEvents<TEvent>(relation).FirstOrDefault();
}
