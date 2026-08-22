using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

/// <summary>
/// Covers the log fields a damage event carries that the parser reads straight off the wire, using the
/// same options the client configures so a property that only binds under looser settings fails here.
/// </summary>
public sealed class DamageEventDeserializationTests
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

    private static DamageEvent Read(string json) =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(json), JsonContext.Event)
            .ShouldBeOfType<DamageEvent>();

    [Fact]
    public void Tick_IsTrueOnAPeriodicHit()
    {
        var e = Read("""
            {"timestamp":300,"type":"damage","sourceID":3,"targetID":109,"abilityGameID":1003047,
             "amount":1234,"mitigated":500,"absorbed":10,"blocked":0,"unmitigatedAmount":1744,"tick":true}
            """);

        e.Tick.ShouldBeTrue();
        e.Amount.ShouldBe(1234);
        e.Mitigated.ShouldBe(500);
        e.Absorbed.ShouldBe(10);
        e.UnmitigatedAmount.ShouldBe(1744);
    }

    [Fact]
    public void Tick_IsFalseWhenTheLogOmitsIt()
    {
        var e = Read("""
            {"timestamp":300,"type":"damage","sourceID":3,"targetID":109,"abilityGameID":2190,"amount":1234}
            """);

        e.Tick.ShouldBeFalse();
    }

    [Fact]
    public void Tick_RoundTripsThroughSerialization()
    {
        var original = new DamageEvent { Timestamp = 300, AbilityGameId = 2190, Amount = 5, Tick = true };

        var json = JsonSerializer.Serialize(original, JsonContext.Event);

        json.ShouldContain("\"tick\":true");
        Read(json).Tick.ShouldBeTrue();
    }

    [Fact]
    public void School_ReadsFromTheAbilityIdAndFailsClosedWithoutAClassification()
    {
        var physical = new DamageEvent { AbilityGameId = 2190 };
        physical.School.ShouldBe(MagicSchool.Physical);
        physical.IsPhysical.ShouldBeTrue();
        physical.IsMagic.ShouldBeFalse();

        var both = new DamageEvent { AbilityGameId = 255 };
        both.IsPhysical.ShouldBeTrue();
        both.IsMagic.ShouldBeTrue();

        var unclassified = new DamageEvent { AbilityGameId = 5 };
        unclassified.School.ShouldBe(default);
        unclassified.IsPhysical.ShouldBeFalse();
        unclassified.IsMagic.ShouldBeFalse();
    }
}
