using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

public sealed class EventDeserializationTests
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

    [Fact]
    public void Deserialize_ShouldProduceStronglyTypedEvents()
    {
        var events = DeserializeFixtureEvents();

        events.ShouldNotBeEmpty();
        events.OfType<CastEvent>().ShouldNotBeEmpty();
        events.OfType<DamageEvent>().ShouldNotBeEmpty();
        events.OfType<HealEvent>().ShouldNotBeEmpty();
        events.OfType<CombatantInfoEvent>().ShouldNotBeEmpty();
        events.OfType<ApplyBuffEvent>().ShouldNotBeEmpty();
        events.OfType<RemoveBuffEvent>().ShouldNotBeEmpty();
    }

    [Fact]
    public void Deserialize_ShouldMatchRimeCastCounts()
    {
        var events = DeserializeFixtureEvents();

        var casts = events.OfType<CastEvent>()
            .Where(e => e.SourceId == 3)
            .GroupBy(e => e.Ability.Guid)
            .ToDictionary(g => g.Key, g => g.Count());

        casts[1018].ShouldBe(254, "Ice Comet casts");
        casts[1028].ShouldBe(299, "Glacial Blast casts");
        casts[1031].ShouldBe(184, "Bursting Ice casts");
        casts[155].ShouldBe(12, "Voidbringer's Touch casts");
        casts[1020].ShouldBe(176, "Cold Snap casts");
        casts[1027].ShouldBe(67, "Freezing Torrent casts");
        casts[1029].ShouldBe(490, "Frost Bolt casts");
    }

    [Fact]
    public void Deserialize_ShouldMatchCombatantStats()
    {
        var events = DeserializeFixtureEvents();
        var combatant = events.OfType<CombatantInfoEvent>().First(e => e.SourceId == 3);

        combatant.ComputedItemLevel.ShouldBe(330m);
        combatant.Intellect.ShouldBe(2476);
        combatant.Stamina.ShouldBe(5267);
        combatant.Armor.ShouldBe(4402);
        combatant.Crit.ShouldBe(875);
        combatant.Haste.ShouldBe(1304);
        combatant.Expertise.ShouldBe(1725);
        combatant.Spirit.ShouldBe(1255);

        combatant.Amethyst.ShouldBe(1206);
        combatant.Emerald.ShouldBe(486);
        combatant.Sapphire.ShouldBe(2916);

        var weapon = combatant.Gear.Single(g => g.Name == "Void-touched Conduit");
        weapon.ItemLevel.ShouldBe(330);
        weapon.Attributes.Single(a => a.Name == "Intellect").Value.ShouldBe(178);
        weapon.Attributes.Single(a => a.Name == "Haste").Value.ShouldBe(200);
        weapon.Attributes.Single(a => a.Name == "Expertise").Value.ShouldBe(245);

        var head = combatant.Gear.Single(g => g.Name == "Headdress of the Nightshriek");
        head.Gem.ShouldNotBeNull();
        head.Gem!.Name.ShouldBe("Splendid Sapphire");
        head.Attributes.Single(a => a.Name == "Haste").Value.ShouldBe(234);
        head.Attributes.Single(a => a.Name == "Spirit").Value.ShouldBe(100);
    }

    [Fact]
    public void Deserialize_ShouldMatchDamageEventTotals()
    {
        var events = DeserializeFixtureEvents();

        var damageByAbility = events.OfType<DamageEvent>()
            .Where(e => e.SourceId == 3 && e.TargetId != 3)
            .GroupBy(e => e.Ability.Guid)
            .ToDictionary(g => g.Key, g => (Damage: g.Sum(e => e.Amount), Hits: g.Count()));

        damageByAbility[1018].Damage.ShouldBe(125_361_701);
        damageByAbility[1018].Hits.ShouldBe(2884);

        damageByAbility[1028].Damage.ShouldBe(37_723_545);
        damageByAbility[1028].Hits.ShouldBe(215);

        damageByAbility[1033].Damage.ShouldBe(7_874_143);
        damageByAbility[1033].Hits.ShouldBe(1658);

        damageByAbility[1000104].Damage.ShouldBe(6_272_149);
        damageByAbility[1001365].Damage.ShouldBe(1_748_769);
    }

    [Fact]
    public void Deserialize_CastEvents_ShouldHavePopulatedAbility()
    {
        var events = DeserializeFixtureEvents();
        var casts = events.OfType<CastEvent>().ToList();

        casts.ShouldNotBeEmpty();
        foreach (var cast in casts)
        {
            cast.Ability.ShouldNotBeNull($"CastEvent at timestamp {cast.Timestamp} has null Ability");
            cast.Ability.Guid.ShouldBeGreaterThan(0, $"CastEvent at timestamp {cast.Timestamp} has Ability.Guid == 0");
            cast.Ability.Name.ShouldNotBeNullOrEmpty($"CastEvent at timestamp {cast.Timestamp} has empty Ability.Name");
        }
    }

    /// <summary>
    /// Reproduces the exact WASM client-side deserialization path:
    /// server serializes EventsResult to JSON, WASM deserializes with Web options.
    /// </summary>
    [Fact]
    public void Deserialize_WithWasmClientOptions_ShouldHavePopulatedAbility()
    {
        // Exactly mirrors FellowshipAnalyzer/Program.cs
        var wasmOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
        };
        wasmOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));

        // Server-side path: deserialize from raw fixture and wrap as EventsResult
        var serverEvents = DeserializeFixtureEvents();
        var eventsResult = new EventsResult(serverEvents, false);

        // Simulate the API endpoint: server serializes EventsResult to JSON
        var json = JsonSerializer.Serialize(eventsResult, JsonContext.EventsResult);

        // WASM client: deserialize EventsResult from the API response
        var result = JsonSerializer.Deserialize<EventsResult>(json, wasmOptions)!;
        var events = result.Events;

        events.ShouldNotBeEmpty();

        var casts = events.OfType<CastEvent>().ToList();
        casts.ShouldNotBeEmpty();
        foreach (var cast in casts)
        {
            cast.Ability.ShouldNotBeNull($"CastEvent at timestamp {cast.Timestamp} has null Ability");
            cast.Ability.Guid.ShouldBeGreaterThan(0);
            cast.Ability.Name.ShouldNotBeNullOrEmpty();
        }

        var damageEvents = events.OfType<DamageEvent>().ToList();
        damageEvents.ShouldNotBeEmpty();
        foreach (var dmg in damageEvents)
        {
            dmg.Ability.ShouldNotBeNull($"DamageEvent at timestamp {dmg.Timestamp} has null Ability");
        }
    }

    private static List<Event> DeserializeFixtureEvents()
    {
        var json = File.ReadAllText(GetFixturePath());
        using var doc = JsonDocument.Parse(json);
        var eventsEl = doc.RootElement
            .GetProperty("data")
            .GetProperty("reportData")
            .GetProperty("report")
            .GetProperty("events")
            .GetProperty("data");
        return JsonSerializer.Deserialize(eventsEl, JsonContext.ListEvent)!;
    }

    private static string GetFixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", "events-with-ability-details.json");
    }
}
