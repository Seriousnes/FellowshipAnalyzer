using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

/// <summary>
/// Covers the <c>duration</c> field of buff and debuff events, using the same options the client configures.
/// </summary>
public sealed class BuffEventDeserializationTests
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

    private static T Read<T>(string json) where T : BuffEvent =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(json), JsonContext.Event)
            .ShouldBeOfType<T>();

    [Fact]
    public void ApplyBuff_HasTheLoggedDurationAndItsEnd()
    {
        var e = Read<ApplyBuffEvent>("""
            {"timestamp":1623967,"type":"applybuff","sourceID":2,"targetID":3,"abilityGameID":1002736,"fight":4,"duration":8000,"absorb":34448}
            """);

        e.Duration.ShouldBe(8000);
        e.ExpectedEnd.ShouldBe(1_631_967);
    }

    [Fact]
    public void RefreshBuff_HasTheLoggedDuration()
    {
        var e = Read<RefreshBuffEvent>("""
            {"timestamp":1623967,"type":"refreshbuff","sourceID":2,"targetID":3,"abilityGameID":1002736,"fight":4,"duration":8000,"absorb":34448}
            """);

        e.Duration.ShouldBe(8000);
    }

    [Fact]
    public void ApplyDebuff_WithTheInfiniteDuration_HasNoExpectedEnd()
    {
        var e = Read<ApplyDebuffEvent>("""
            {"timestamp":1623967,"type":"applydebuff","sourceID":2,"targetID":3,"abilityGameID":1002736,"fight":4,"duration":9999000,"absorb":34448}
            """);

        e.Duration.ShouldBe(9_999_000);
        e.ExpectedEnd.ShouldBeNull();
    }

    [Fact]
    public void RemoveBuff_HasNoDurationAndNoExpectedEnd()
    {
        var e = Read<RemoveBuffEvent>("""
            {"timestamp":1,"type":"removebuff","sourceID":2,"targetID":3,"abilityGameID":1002736,"fight":4}
            """);

        e.Duration.ShouldBeNull();
        e.ExpectedEnd.ShouldBeNull();
    }
}
