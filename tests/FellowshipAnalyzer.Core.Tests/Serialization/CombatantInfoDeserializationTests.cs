using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

/// <summary>
/// Covers the gear detail a combatantinfo reports that a stat effect's magnitude is sized from, using the
/// same options the client configures so a property that only binds under looser settings fails here. The
/// JSON is copied from a real report's combatantinfo gear entry.
/// </summary>
public sealed class CombatantInfoDeserializationTests
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

    private static CombatantInfoEvent Read(string json) =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(json), JsonContext.Event)
            .ShouldBeOfType<CombatantInfoEvent>();

    private const string GearedCombatant = """
        {"timestamp":4794579,"type":"combatantinfo","sourceID":54,"haste":65,"intellect":148,
         "gear":[
           {"id":5233,"quality":3,"name":"Heig's Chain","itemLevel":60,
            "attributes":[{"id":14,"name":"Haste","value":6}],
            "traits":[{"id":3000036,"rank":1,"icon":"T_Nhance_RPG_Fire_19.jpg","name":"Iron Spikes"}]},
           {"id":5288,"quality":3,"name":"Fervent Zealot's Epaulets","itemLevel":135,
            "attributes":[{"id":15,"name":"Expertise","value":11}],
            "traits":[{"id":3000041,"rank":2,"name":"Stalwart Readiness"},
                      {"id":3000029,"rank":1,"name":"First Man Standing"}]},
           {"id":5134,"quality":2,"name":"Conjurer's Silken Cloak","itemLevel":105,
            "blessings":[{"id":4000161,"level":1,"name":"The Mystic"}]},
           {"id":5390,"quality":1,"name":"Baneful Tempest Bracers","itemLevel":30,
            "set":{"id":682,"name":"Sin Warding"},
            "blessings":[{"id":4000043,"level":2,"name":"The Wayfarer"}]}
         ],
         "auras":[{"source":54,"ability":1000105,"stacks":1,"name":"Heart of Stone"}]}
        """;

    [Fact]
    public void GearTraits_BindWithTheirRank()
    {
        var info = Read(GearedCombatant);

        info.Gear[0].Traits.ShouldHaveSingleItem();
        info.Gear[0].Traits[0].Id.ShouldBe(3000036);
        info.Gear[0].Traits[0].Rank.ShouldBe(1);
        info.Gear[0].Traits[0].Name.ShouldBe("Iron Spikes");

        info.Gear[1].Traits.Count.ShouldBe(2);
        info.Gear[1].Traits[0].Rank.ShouldBe(2);
    }

    [Fact]
    public void GearBlessings_BindWithTheirLevel()
    {
        var info = Read(GearedCombatant);

        info.Gear[2].Blessings.ShouldHaveSingleItem();
        info.Gear[2].Blessings[0].Id.ShouldBe(4000161);
        info.Gear[2].Blessings[0].Level.ShouldBe(1);
        info.Gear[2].Blessings[0].Name.ShouldBe("The Mystic");
    }

    [Fact]
    public void TraitRank_TakesTheHighestRankAcrossGear()
    {
        var combatant = new FullCombatant(Read(GearedCombatant));

        combatant.TraitRank(3000041).ShouldBe(2);
        combatant.TraitRank(3000036).ShouldBe(1);
        combatant.TraitRank(3000004).ShouldBe(0);
    }

    [Fact]
    public void BlessingLevel_MatchesOnTheBlessingName()
    {
        var combatant = new FullCombatant(Read(GearedCombatant));

        combatant.BlessingLevel("The Wayfarer").ShouldBe(2);
        combatant.BlessingLevel("The Mystic").ShouldBe(1);
        combatant.BlessingLevel("The Trickster").ShouldBe(0);
    }

    [Fact]
    public void Auras_CarryTheAbilityAndStackCountThePlayerAlreadyHad()
    {
        var info = Read(GearedCombatant);

        info.Auras.ShouldHaveSingleItem();
        info.Auras[0].Ability.ShouldBe(1000105);
        info.Auras[0].Stacks.ShouldBe(1);
    }
}
