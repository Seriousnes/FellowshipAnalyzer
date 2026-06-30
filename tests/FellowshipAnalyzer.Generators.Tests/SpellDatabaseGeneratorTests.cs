using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class SpellDatabaseGeneratorTests
{
    private const string SpellDb = """
        {
          "rime": {
            "FreezingTorrent": { "id": 1027, "name": "Freezing Torrent", "icon": "T_Rime_ChanneledBeam.jpg", "cooldown": 15, "range": 30, "channelDuration": 2.0, "channelTickInterval": 0.4 },
            "GlacialBlast": { "id": 1028, "name": "Glacial Blast", "icon": "T_Rime_AnimaBolt.jpg", "costs": { "tertiary": 2 } },
            "BurstingIceDamage": { "id": 1396, "kind": "effect", "name": "Bursting Ice", "icon": "T_Rime_CastedDebuffAOEdamage.jpg" }
          },
          "shared": {
            "Chronoshift": { "id": 1558, "name": "Chronoshift", "icon": "T_Nhance_RPG_Icons_ArcaneLoad.jpg" }
          },
          "items": {
            "VoidbringerTouch": { "id": 155, "name": "Voidbringer's Touch", "icon": "T_Weapon_VoidTouch.jpg", "cooldown": 90, "range": 30 }
          }
        }
        """;

    [Fact]
    public void Emits_PerScopeRegistries_WithTypedMembersAndScalars()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDb).ConcatenatedGenerated;
        gen.ShouldContain("class Spells"); // namespace ...Spells.Rime
        gen.ShouldContain("FreezingTorrent");
        gen.ShouldContain("Cooldown = 15");
        gen.ShouldContain("ChannelTickInterval = 0.4");
        gen.ShouldContain("Costs = new");
        gen.ShouldContain("ResourceTypes.Tertiary] = 2");
        gen.ShouldContain("new Effect"); // BurstingIceDamage typed by kind
        gen.ShouldContain("class Items");
        gen.ShouldContain("public const int FreezingTorrent = 1027;");
        gen.ShouldContain("public const int BurstingIceDamage = 1001396;");
        gen.ShouldContain("public const int VoidbringerTouch = 155;");
    }

    [Fact]
    public void Aggregates_All_ByGuid()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDb).ConcatenatedGenerated;
        gen.ShouldContain("FrozenDictionary");
        gen.ShouldContain(".Guid");
    }
}
