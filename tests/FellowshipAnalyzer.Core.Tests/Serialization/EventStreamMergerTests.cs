using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

public sealed class EventStreamMergerTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateDeserializerOptions();
    private static readonly FellowshipAnalyzerJsonContext JsonContext = new(JsonOptions);

    private const string PlayerStream = """
        {"inProgress":false,"events":[
          {"timestamp":100,"type":"cast","sourceID":3,"abilityGameID":1018},
          {"timestamp":150,"type":"death","sourceID":3,"targetID":10},
          {"timestamp":300,"type":"damage","sourceID":3,"abilityGameID":1018}
        ]}
        """;

    // Same fight, all deaths regardless of killer/hostility. The 150 enemy death carries a
    // targetInstance the player-stream copy lacks, and sources 2/5 never appear in the player stream.
    private const string DeathStream = """
        {"inProgress":false,"events":[
          {"timestamp":150,"type":"death","sourceID":3,"targetID":10,"targetInstance":1},
          {"timestamp":150,"type":"death","sourceID":5,"targetID":11},
          {"timestamp":90,"type":"death","sourceID":2,"targetID":12}
        ]}
        """;

    [Fact]
    public void Merge_DropsPlayerDeaths_AndUsesDeathStreamAsSourceOfTruth()
    {
        var combined = EventStreamMerger.Merge(Bytes(PlayerStream), Bytes(DeathStream));

        using var doc = JsonParse(combined);
        var events = doc.RootElement.GetProperty("events");

        // 2 non-death player events + 3 death-stream deaths; the player-stream death is dropped.
        events.GetArrayLength().ShouldBe(5);
        var deaths = events.EnumerateArray().Where(e => e.GetProperty("type").GetString() == "death").ToList();
        deaths.Count.ShouldBe(3);

        // Deaths credited to other players (2, 5) are present — proof it is not just the player's kills.
        var deathSources = deaths.Select(d => d.GetProperty("sourceID").GetInt32()).ToList();
        deathSources.ShouldContain(2);
        deathSources.ShouldContain(5);

        // The surviving sourceID:3 death is the death-stream copy (has targetInstance), not the
        // stripped player-stream copy (which had none).
        var source3Death = deaths.Single(d => d.GetProperty("sourceID").GetInt32() == 3);
        source3Death.TryGetProperty("targetInstance", out _).ShouldBeTrue();
    }

    [Fact]
    public void Merge_OrdersEventsByTimestamp()
    {
        var combined = EventStreamMerger.Merge(Bytes(PlayerStream), Bytes(DeathStream));

        using var doc = JsonParse(combined);
        var timestamps = doc.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(e => e.GetProperty("timestamp").GetInt32())
            .ToList();

        timestamps.ShouldBe([90, 100, 150, 150, 300]);
    }

    [Fact]
    public void Merge_CarriesInProgressFromPlayerStream()
    {
        var inProgressPlayer = PlayerStream.Replace("\"inProgress\":false", "\"inProgress\":true");

        var combined = EventStreamMerger.Merge(Bytes(inProgressPlayer), Bytes(DeathStream));

        using var doc = JsonParse(combined);
        doc.RootElement.GetProperty("inProgress").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Merge_ProducesPayloadThatDeserializesToTypedEvents()
    {
        var combined = EventStreamMerger.Merge(Bytes(PlayerStream), Bytes(DeathStream));

        using var doc = JsonParse(combined);
        var events = doc.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(e => JsonSerializer.Deserialize(e.GetRawText(), JsonContext.Event)!)
            .ToList();

        events.OfType<DeathEvent>().Count().ShouldBe(3);
        events.OfType<CastEvent>().Count().ShouldBe(1);
        events.OfType<DamageEvent>().Count().ShouldBe(1);
    }

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static JsonDocument JsonParse(byte[] bytes) => JsonDocument.Parse(bytes);

    private static JsonSerializerOptions CreateDeserializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        return options;
    }
}
