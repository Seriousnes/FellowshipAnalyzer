using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>The byte range of one event object within an <c>{ inProgress, events }</c> payload, and the metadata to deserialize it with.</summary>
public readonly record struct EventJsonRange(int Start, int Length, JsonTypeInfo TypeInfo);

/// <summary>The <c>inProgress</c> flag and the located event ranges of an <c>{ inProgress, events }</c> payload.</summary>
public sealed record EventsResultMetadata(bool InProgress, List<EventJsonRange> EventRanges);

/// <summary>
/// Locates each event object inside an <c>{ inProgress, events }</c> payload and deserializes them one
/// at a time, so the caller can report progress and yield between events.
/// <para>
/// The scan resolves each event's concrete type from its <c>type</c> discriminator as it passes over the
/// object, which lets <see cref="ReadEvent"/> deserialize against the derived type rather than incurring
/// polymorphic dispatch on a base <see cref="Event"/> that would have to find the same discriminator again.
/// </para>
/// </summary>
public static class EventStreamReader
{
    private static ReadOnlySpan<byte> DiscriminatorPropertyName => "type"u8;

    /// <summary>
    /// Reads the payload's <c>inProgress</c> flag and locates every event object in its <c>events</c> array.
    /// Throws when the payload is malformed, or contains an event whose <c>type</c> has no registered subclass.
    /// </summary>
    public static EventsResultMetadata ReadMetadata(byte[] eventsResultJson, FellowshipAnalyzerJsonContext jsonContext)
    {
        var reader = new Utf8JsonReader(eventsResultJson);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("Event data was not a JSON object.");
        }

        bool? inProgress = null;
        List<EventJsonRange>? eventRanges = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new InvalidOperationException($"Unexpected token in event data: {reader.TokenType}.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Event data ended while reading a property value.");
            }

            switch (propertyName)
            {
                case "inProgress":
                case "InProgress":
                    if (reader.TokenType is not JsonTokenType.True and not JsonTokenType.False)
                    {
                        throw new InvalidOperationException("Event data inProgress value was not a boolean.");
                    }
                    inProgress = reader.GetBoolean();
                    break;
                case "events":
                case "Events":
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        throw new InvalidOperationException("Event data events value was not an array.");
                    }
                    eventRanges = ReadEventRanges(ref reader, eventsResultJson, jsonContext);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new EventsResultMetadata(
            inProgress ?? throw new InvalidOperationException("Event data did not include inProgress."),
            eventRanges ?? throw new InvalidOperationException("Event data did not include events."));
    }

    /// <summary>Deserializes the single event located at <paramref name="range"/>.</summary>
    public static Event ReadEvent(byte[] eventsResultJson, EventJsonRange range)
    {
        return JsonSerializer.Deserialize(eventsResultJson.AsSpan(range.Start, range.Length), range.TypeInfo) as Event
            ?? throw new InvalidOperationException("Event data contained a null event.");
    }

    private static List<EventJsonRange> ReadEventRanges(
        ref Utf8JsonReader reader,
        byte[] eventsResultJson,
        FellowshipAnalyzerJsonContext jsonContext)
    {
        var ranges = new List<EventJsonRange>();
        var typeInfoByEventType = new Dictionary<Type, JsonTypeInfo>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return ranges;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"Unexpected token in events array: {reader.TokenType}.");
            }

            var start = checked((int)reader.TokenStartIndex);
            reader.Skip();
            var length = checked((int)reader.BytesConsumed) - start;

            var typeInfo = ResolveTypeInfo(eventsResultJson.AsSpan(start, length), jsonContext, typeInfoByEventType);
            ranges.Add(new EventJsonRange(start, length, typeInfo));
        }

        throw new InvalidOperationException("Event data ended while reading the events array.");
    }

    /// <summary>
    /// Reads one event object's discriminator and returns the metadata for its concrete type. Stops at the
    /// discriminator rather than reading the whole object, since the upstream stream writes it near the front.
    /// </summary>
    private static JsonTypeInfo ResolveTypeInfo(
        ReadOnlySpan<byte> eventObject,
        FellowshipAnalyzerJsonContext jsonContext,
        Dictionary<Type, JsonTypeInfo> typeInfoByEventType)
    {
        var reader = new Utf8JsonReader(eventObject);
        reader.Read();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new InvalidOperationException($"Unexpected token in event object: {reader.TokenType}.");
            }

            var isDiscriminator = reader.ValueTextEquals(DiscriminatorPropertyName);
            reader.Read();

            if (!isDiscriminator)
            {
                reader.Skip();
                continue;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidOperationException("Event type discriminator was not a string.");
            }

            if (!EventDiscriminators.TryGetType(reader.ValueSpan, out var eventType))
            {
                throw new InvalidOperationException($"Unknown event type '{reader.GetString()}'.");
            }

            if (!typeInfoByEventType.TryGetValue(eventType, out var typeInfo))
            {
                typeInfo = jsonContext.GetTypeInfo(eventType)
                    ?? throw new InvalidOperationException($"No serialization metadata for event type '{eventType}'.");
                typeInfoByEventType[eventType] = typeInfo;
            }

            return typeInfo;
        }

        throw new InvalidOperationException("Event data contained an event without a type discriminator.");
    }
}
