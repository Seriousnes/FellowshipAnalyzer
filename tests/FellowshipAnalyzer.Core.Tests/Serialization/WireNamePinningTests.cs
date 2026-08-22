using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

public sealed class WireNamePinningTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateDeserializerOptions();
    private static readonly FellowshipAnalyzerJsonContext JsonContext = new(JsonOptions);

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

    private static T Read<T>(string json) where T : Event =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(json), JsonContext.Event)
            .ShouldBeOfType<T>();

    [Fact]
    public void DungeonId_BindsToTheRawFightField()
    {
        var e = Read<DamageEvent>("""
            {"timestamp":300,"type":"damage","fight":4,"sourceID":3,"targetID":109,
             "abilityGameID":2190,"amount":1234}
            """);

        e.DungeonId.ShouldBe(4);
    }

    [Fact]
    public void DungeonId_IsZeroWhenTheLogOmitsTheFightField()
    {
        var e = Read<DamageEvent>("""
            {"timestamp":300,"type":"damage","sourceID":3,"targetID":109,
             "abilityGameID":2190,"amount":1234}
            """);

        e.DungeonId.ShouldBe(0);
    }

    [Fact]
    public void DungeonId_DoesNotBindToADungeonIdField()
    {
        var e = Read<DamageEvent>("""
            {"timestamp":300,"type":"damage","dungeonId":4,"sourceID":3,"targetID":109,
             "abilityGameID":2190,"amount":1234}
            """);

        e.DungeonId.ShouldBe(0);
    }

    [Fact]
    public void SerializedDungeonId_KeepsTheRawFightName()
    {
        var json = JsonSerializer.Serialize(
            new DamageEvent { Timestamp = 300, DungeonId = 4 }, JsonContext.Event);

        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("fight", out var fight).ShouldBeTrue();
        fight.GetInt32().ShouldBe(4);
        document.RootElement.TryGetProperty("dungeonId", out _).ShouldBeFalse();
    }
}
