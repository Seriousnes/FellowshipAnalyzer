using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

/// <summary>
/// Pins <see cref="FSLID"/>'s type-level <c>[JsonConverter(typeof(FSLIDJsonConverter))]</c> as honored
/// by the source-generated <see cref="FellowshipAnalyzerJsonContext"/> resolver, deserializing and
/// serializing through its generated <c>JsonTypeInfo</c> rather than a bare
/// <c>JsonSerializer.Deserialize&lt;T&gt;(string)</c> call. The bare overload can silently fall back to
/// desktop reflection metadata and would pass even if the AOT-safe resolver never picked up the
/// converter. <see cref="CreateClientJsonContext"/> mirrors the exact context construction the WASM
/// client performs in <c>Program.cs</c>, so this exercises the code path AOT actually runs at
/// combat-log parse time.
/// </summary>
public sealed class FSLIDSourceGenRoundTripTests
{
    private static readonly FellowshipAnalyzerJsonContext JsonContext = CreateClientJsonContext();

    private const string EventsJson = """
        [
          {
            "type": "damage",
            "timestamp": 1000,
            "fight": 1,
            "sourceID": 1,
            "targetID": 2,
            "amount": 500,
            "ability": {
              "name": "Test Effect",
              "guid": 1001396,
              "abilityIcon": "icon.jpg"
            },
            "abilityGameID": 2000042
          }
        ]
        """;

    private static FellowshipAnalyzerJsonContext CreateClientJsonContext()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        return new FellowshipAnalyzerJsonContext(options);
    }

    [Fact]
    public void Deserialize_ThroughSourceGenContext_DecodesNestedAndFlatFSLID()
    {
        var events = JsonSerializer.Deserialize(EventsJson, JsonContext.ListEvent)!;
        var damageEvent = events.OfType<DamageEvent>().Single();

        damageEvent.Ability.FSLID.Value.ShouldBe(1_001_396);
        damageEvent.Ability.FSLID.Kind.ShouldBe(SpellKind.Effect);
        damageEvent.Ability.FSLID.NativeId.ShouldBe(1_396);

        damageEvent.AbilityGameId.Value.ShouldBe(2_000_042);
        damageEvent.AbilityGameId.Kind.ShouldBe(SpellKind.Talent);
        damageEvent.AbilityGameId.NativeId.ShouldBe(42);
    }

    /// <summary>
    /// Proves the resolver invokes <see cref="FSLIDJsonConverter"/> on write, not just read: without
    /// it, source gen would serialize the struct's public getters as an object
    /// (<c>{"value":...,"kind":...,"nativeId":...}</c>) instead of a bare number.
    /// </summary>
    [Fact]
    public void SerializeThenDeserialize_ThroughSourceGenContext_RoundTripsAsNumber()
    {
        var events = JsonSerializer.Deserialize(EventsJson, JsonContext.ListEvent)!;

        var json = JsonSerializer.Serialize(events, JsonContext.ListEvent);
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement[0];

        var abilityGuid = element.GetProperty("ability").GetProperty("guid");
        abilityGuid.ValueKind.ShouldBe(JsonValueKind.Number);
        abilityGuid.GetInt32().ShouldBe(1_001_396);

        var abilityGameId = element.GetProperty("abilityGameId");
        abilityGameId.ValueKind.ShouldBe(JsonValueKind.Number);
        abilityGameId.GetInt32().ShouldBe(2_000_042);

        var roundTripped = JsonSerializer.Deserialize(json, JsonContext.ListEvent)!
            .OfType<DamageEvent>().Single();
        roundTripped.Ability.FSLID.Value.ShouldBe(1_001_396);
        roundTripped.AbilityGameId.Value.ShouldBe(2_000_042);
    }
}
