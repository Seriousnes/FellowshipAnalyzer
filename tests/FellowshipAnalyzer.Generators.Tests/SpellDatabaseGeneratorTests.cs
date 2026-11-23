using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class SpellDatabaseGeneratorTests
{
    private const string SpellDb = """
        {
          "rime": {
            "FreezingTorrent": { "id": 1027, "name": "Freezing Torrent", "icon": "T_Rime_ChanneledBeam.jpg", "abilityCategory": "Core", "cooldown": 15, "range": 30, "channelDuration": 2.0, "channelTickInterval": 0.4 },
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
        gen.ShouldContain("class Spells");
        gen.ShouldContain("FreezingTorrent");
        gen.ShouldContain("Cooldown = 15");
        gen.ShouldContain("AbilityCategory = global::FellowshipAnalyzer.Core.Common.Spells.AbilityCategory.Core");
        gen.ShouldContain("ChannelTickInterval = 0.4");
        gen.ShouldContain("Costs = new");
        gen.ShouldContain("ResourceTypes.Tertiary] = 2");
        gen.ShouldContain("new global::FellowshipAnalyzer.Core.Common.Spells.Effect");
        gen.ShouldContain("namespace FellowshipAnalyzer.Core.Common.Items;");
        gen.ShouldContain("class Items");
        gen.ShouldContain("[global::FellowshipAnalyzer.Core.Common.Spells.SpellId(1027)]");
        gen.ShouldContain("[global::FellowshipAnalyzer.Core.Common.Spells.SpellId(1001396)]");
        gen.ShouldContain("[global::FellowshipAnalyzer.Core.Common.Spells.SpellId(155)]");
        gen.ShouldNotContain("class Guids");
    }

    [Fact]
    public void Aggregates_All_ByFslid()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDb).ConcatenatedGenerated;
        gen.ShouldContain("FrozenDictionary");
        gen.ShouldContain(".FSLID.Value] =");
    }

    private const string SpellsNamespace = "global::FellowshipAnalyzer.Core.Common.Spells";

    private static string SpellDbOf(string entry) => "{ \"rime\": { \"ColdSnap\": " + entry + " } }";

    private static string Generate(string entry)
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbOf(entry));
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        return run.ConcatenatedGenerated;
    }

    [Fact]
    public void Emits_CooldownScaling_AsANestedRecord() =>
        Generate("""{ "id": 1020, "name": "Cold Snap", "icon": "x", "cooldownScaling": { "durationHaste": true } }""")
            .ShouldContain($"CooldownScaling = new {SpellsNamespace}.CooldownScaling {{ DurationHaste = true }}");

    [Fact]
    public void Emits_AChargeGrantsCooldownScaling_AsARecordNestedInTheRecord() =>
        Generate("""{ "id": 1020, "name": "Cold Snap", "icon": "x", "chargeGrant": { "source": "LegendaryItem", "id": 5898, "charges": 2, "cooldownScaling": { "timeRateCooldownRecovery": true, "timeRateHaste": true } } }""")
            .ShouldContain(
                $"ChargeGrant = new {SpellsNamespace}.ChargeGrant {{ Source = {SpellsNamespace}.GrantSource.LegendaryItem, Id = 5898, Charges = 2, "
                + $"CooldownScaling = new {SpellsNamespace}.CooldownScaling {{ TimeRateCooldownRecovery = true, TimeRateHaste = true }} }}");

    [Fact]
    public void Emits_TheGlobalCooldownAndItsHasteScaling()
    {
        var generated = Generate("""{ "id": 1020, "name": "Cold Snap", "icon": "x", "globalCooldown": 1, "globalCooldownScalesWithHaste": false }""");

        generated.ShouldContain("GlobalCooldown = 1");
        generated.ShouldContain("GlobalCooldownScalesWithHaste = false");
    }

    [Fact]
    public void Reports_TheRetiredCooldownHasteKey_AsUnknown()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbOf("""{ "id": 1020, "name": "Cold Snap", "icon": "x", "cooldownHaste": "Duration" }"""));

        run.DriverDiagnostics.ShouldContain(d => d.Id == "FA0009" && d.GetMessage(null).Contains("'cooldownHaste'"));
    }

    private const string SpellDbWithLegendaries = """
        {
          "aeona": {
            "Oblivion": { "id": 1970, "name": "Oblivion", "icon": "T_Lisa_ChronaBlast.jpg" }
          },
          "legendaries": {
            "aeona": {
              "MassEntropy": { "itemId": 5226, "itemName": "Bands of the Withering Shores", "powerId": 620, "powerName": "Mass Entropy", "slot": "Wrists", "icon": "Tex_bracelets_cl_01_bg" }
            }
          }
        }
        """;

    [Fact]
    public void Emits_LegendaryItemsPerHero_NamedForThePowerTheyGrant()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithLegendaries).ConcatenatedGenerated;

        gen.ShouldContain("namespace FellowshipAnalyzer.Core.Common.Spells.Aeona;");
        gen.ShouldContain("public partial class Legendaries : global::FellowshipAnalyzer.Core.Common.Items.IItemRegistry");
        gen.ShouldContain("public static global::FellowshipAnalyzer.Core.Common.Items.Item MassEntropy { get; } = new(5226, \"Bands of the Withering Shores\", \"Tex_bracelets_cl_01_bg\", global::FellowshipAnalyzer.Core.Analysis.HeroName.Aeona) { PowerName = \"Mass Entropy\" };");
        gen.ShouldContain("public static class AeonaLegendaries");
        gen.ShouldContain("public const int MassEntropy = 5226;");
    }
}
