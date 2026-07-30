using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

public sealed class EventStreamReaderTests
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

    private const string Payload = """
        {"inProgress":false,"events":[
          {"timestamp":100,"type":"cast","sourceID":3,"abilityGameID":1018},
          {"timestamp":150,"type":"death","sourceID":3,"targetID":10},
          {"timestamp":300,"type":"damage","sourceID":3,"abilityGameID":1018,"amount":1234},
          {"timestamp":400,"type":"applybuff","sourceID":3,"targetID":3,"abilityGameID":1020}
        ]}
        """;

    [Fact]
    public void ReadMetadata_ResolvesInProgressAndOneRangePerEvent()
    {
        var bytes = Bytes(Payload);

        var metadata = EventStreamReader.ReadMetadata(bytes, JsonContext);

        metadata.InProgress.ShouldBeFalse();
        metadata.EventRanges.Count.ShouldBe(4);
    }

    [Fact]
    public void ReadEvent_ProducesTheSameEventsAsPolymorphicDeserialization()
    {
        var bytes = Bytes(Payload);

        var metadata = EventStreamReader.ReadMetadata(bytes, JsonContext);
        var viaReader = metadata.EventRanges.Select(r => EventStreamReader.ReadEvent(bytes, r)).ToList();

        using var doc = JsonDocument.Parse(bytes);
        var viaPolymorphic = doc.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(e => JsonSerializer.Deserialize(e.GetRawText(), JsonContext.Event)!)
            .ToList();

        viaReader.Select(e => e.GetType()).ShouldBe(viaPolymorphic.Select(e => e.GetType()));
        Serialize(viaReader).ShouldBe(Serialize(viaPolymorphic));
    }

    [Fact]
    public void ReadMetadata_ThrowsForAnUnregisteredEventType()
    {
        var bytes = Bytes("""{"inProgress":false,"events":[{"timestamp":1,"type":"teleported"}]}""");

        var ex = Should.Throw<InvalidOperationException>(() => EventStreamReader.ReadMetadata(bytes, JsonContext));

        ex.Message.ShouldContain("teleported");
    }

    [Fact]
    public void ReadMetadata_ThrowsForAnEventWithoutADiscriminator()
    {
        var bytes = Bytes("""{"inProgress":false,"events":[{"timestamp":1,"sourceID":3}]}""");

        Should.Throw<InvalidOperationException>(() => EventStreamReader.ReadMetadata(bytes, JsonContext));
    }

    [Fact]
    public void ReadMetadata_HandlesADiscriminatorAfterNestedObjectsAndArrays()
    {
        var bytes = Bytes("""
            {"inProgress":false,"events":[
              {"timestamp":1,"sourceResources":{"hitPoints":5,"resources":[{"type":"mana","amount":3}]},
               "type":"cast","sourceID":3,"abilityGameID":1018}
            ]}
            """);

        var metadata = EventStreamReader.ReadMetadata(bytes, JsonContext);

        var e = EventStreamReader.ReadEvent(bytes, metadata.EventRanges.Single());
        e.ShouldBeOfType<CastEvent>();
        e.SourceResources.ShouldNotBeNull();
        e.SourceResources!.HitPoints.ShouldBe(5);
    }

    /// <summary>
    /// The generated lookup and the generated <c>JsonDerivedType</c> registrations are two separate emitted
    /// surfaces over the same event classes; this fails if either one drifts.
    /// </summary>
    [Fact]
    public void GeneratedLookup_AgreesWithTheJsonDerivedTypeRegistrations()
    {
        var registrations = typeof(Event)
            .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .ToList();

        registrations.ShouldNotBeEmpty();
        EventDiscriminators.Count.ShouldBe(registrations.Count);

        foreach (var registration in registrations)
        {
            var discriminator = registration.TypeDiscriminator.ShouldBeOfType<string>();

            EventDiscriminators.TryGetType(Encoding.UTF8.GetBytes(discriminator), out var resolved)
                .ShouldBeTrue($"'{discriminator}' has no entry in the generated lookup");
            resolved.ShouldBe(registration.DerivedType);
        }
    }

    [Fact]
    public void GeneratedLookup_RejectsUnknownAndPartialDiscriminators()
    {
        EventDiscriminators.TryGetType("cas"u8, out _).ShouldBeFalse();
        EventDiscriminators.TryGetType("castt"u8, out _).ShouldBeFalse();
        EventDiscriminators.TryGetType("Cast"u8, out _).ShouldBeFalse();
        EventDiscriminators.TryGetType(default, out _).ShouldBeFalse();
    }

    /// <summary>Fabricated events never appear on the wire, so they must not be resolvable from a discriminator.</summary>
    [Fact]
    public void GeneratedLookup_ExcludesFabricatedEvents()
    {
        EventDiscriminators.TryGetType("pullstart"u8, out _).ShouldBeFalse();
        EventDiscriminators.TryGetType("pullend"u8, out _).ShouldBeFalse();
        EventDiscriminators.TryGetType("phase"u8, out _).ShouldBeFalse();
    }

    private static string Serialize(List<Event> events) => JsonSerializer.Serialize(events, JsonContext.ListEvent);

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

}
