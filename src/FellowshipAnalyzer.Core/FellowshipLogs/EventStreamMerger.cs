using System.Text.Json;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Merges the player-scoped event stream with the fight-scoped death stream into a single
/// combined <c>{ inProgress, events }</c> payload for caching and analysis.
/// <para>
/// The fight-scoped death query is the single source of truth for <c>death</c> events, so every
/// <c>death</c> event in the player stream is dropped and replaced by the death stream (which
/// includes deaths credited to any player and both hostilities, not just the selected player's
/// kills). Events are emitted in ascending timestamp order; ties keep player-stream-before-death
/// ordering, matching how the upstream interleaves a kill after its killing blow.
/// </para>
/// </summary>
public static class EventStreamMerger
{
    /// <summary>
    /// Combines a player-scoped <c>{ inProgress, events }</c> payload and a fight-scoped death
    /// payload into a single timestamp-ordered <c>{ inProgress, events }</c> payload. Death events
    /// present in <paramref name="playerEventsJson"/> are dropped in favour of
    /// <paramref name="deathStreamJson"/>. Event objects are copied verbatim.
    /// </summary>
    public static byte[] Merge(byte[] playerEventsJson, byte[] deathStreamJson)
    {
        var entries = new List<Entry>();
        var inProgress = ReadEntries(playerEventsJson, excludeDeaths: true, entries);
        ReadEntries(deathStreamJson, excludeDeaths: false, entries);

        var ordered = entries
            .Select(static (e, i) => (Entry: e, Index: i))
            .OrderBy(static x => x.Entry.Timestamp)
            .ThenBy(static x => x.Index)
            .Select(static x => x.Entry);

        using var buffer = new MemoryStream(playerEventsJson.Length + deathStreamJson.Length);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("inProgress", inProgress);
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var e in ordered)
            {
                writer.WriteRawValue(e.Source.AsSpan(e.Start, e.Length), skipInputValidation: true);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private readonly record struct Entry(byte[] Source, int Start, int Length, int Timestamp);

    private static bool ReadEntries(byte[] json, bool excludeDeaths, List<Entry> entries)
    {
        var reader = new Utf8JsonReader(json);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("Event data was not a JSON object.");
        }

        var inProgress = false;
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

            var isInProgress = reader.ValueTextEquals("inProgress") || reader.ValueTextEquals("InProgress");
            var isEvents = reader.ValueTextEquals("events") || reader.ValueTextEquals("Events");
            if (!reader.Read())
            {
                throw new InvalidOperationException("Event data ended while reading a property value.");
            }

            if (isInProgress)
            {
                inProgress = reader.TokenType == JsonTokenType.True;
            }
            else if (isEvents)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new InvalidOperationException("Event data events value was not an array.");
                }
                ReadEventArray(ref reader, json, excludeDeaths, entries);
            }
            else
            {
                reader.Skip();
            }
        }

        return inProgress;
    }

    private static void ReadEventArray(ref Utf8JsonReader reader, byte[] json, bool excludeDeaths, List<Entry> entries)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return;
            }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"Unexpected token in events array: {reader.TokenType}.");
            }

            var start = checked((int)reader.TokenStartIndex);
            reader.Skip();
            var length = checked((int)reader.BytesConsumed) - start;

            var (timestamp, isDeath) = ReadTimestampAndType(json.AsSpan(start, length));
            if (excludeDeaths && isDeath)
            {
                continue;
            }
            entries.Add(new Entry(json, start, length, timestamp));
        }

        throw new InvalidOperationException("Event data ended while reading the events array.");
    }

    private static (int Timestamp, bool IsDeath) ReadTimestampAndType(ReadOnlySpan<byte> eventObject)
    {
        var reader = new Utf8JsonReader(eventObject);
        reader.Read();

        var timestamp = 0;
        var isDeath = false;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var isTimestamp = reader.ValueTextEquals("timestamp");
            var isType = reader.ValueTextEquals("type");
            reader.Read();

            if (isTimestamp && reader.TokenType == JsonTokenType.Number)
            {
                timestamp = reader.GetInt32();
            }
            else if (isType && reader.TokenType == JsonTokenType.String)
            {
                isDeath = reader.ValueTextEquals("death");
            }
            else
            {
                reader.Skip();
            }
        }

        return (timestamp, isDeath);
    }
}
