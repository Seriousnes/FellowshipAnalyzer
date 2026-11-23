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

    private const string SpellDbWithEffects = """
        {
          "rime": {
            "ColdSnap": { "id": 1020, "name": "Cold Snap", "icon": "x" }
          },
          "effects": {
            "2253": { "duration": 20, "modifiers": [ { "attribute": "Haste", "op": "Additive", "value": 0.3 } ] },
            "2141": { "duration": 8, "maxStacks": 5, "modifiers": [ { "attribute": "HasteRating", "op": "Additive", "value": 4, "ranks": [4, 8, 12, 16] } ] },
            "2719": { "modifiers": [ { "attribute": "CooldownRecovery", "op": "AddFinal", "coefficient": 0.33334 } ] },
            "3216": { "duration": 3, "cooldownAccelerations": [ { "ability": 2295, "value": 2 } ] }
          },
          "ratingCurves": {
            "CritChance": { "baseStatMultiplier": 0.16, "brackets": [ { "maxBracketValue": 10, "penaltyPercentage": 1 } ], "finalPenaltyPercentage": 0.9 },
            "Haste": { "baseStatMultiplier": 0.16, "brackets": [ { "maxBracketValue": 10, "penaltyPercentage": 1 }, { "maxBracketValue": 15, "penaltyPercentage": 0.98 }, { "maxBracketValue": 20, "penaltyPercentage": 0.96 }, { "maxBracketValue": 25, "penaltyPercentage": 0.94 } ], "finalPenaltyPercentage": 0.92 }
          }
        }
        """;

    [Fact]
    public void Emits_EffectData_WithASwitchArmForEachEffect_AndCompiles()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithEffects);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        run.DriverDiagnostics.ShouldNotContain(d => d.Id == "FA0005");
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("public static class EffectData");
        gen.ShouldContain("case 2253: return Modifiers2253;");
        gen.ShouldContain("case 2141: return Modifiers2141;");
        gen.ShouldContain("case 2719: return Modifiers2719;");
        gen.ShouldContain("case 3216: return Accelerations3216;");
        gen.ShouldContain("2253 => 20,");
        gen.ShouldContain("2141 => 5,");
        gen.ShouldContain("new AttributeModifier(StatAttribute.Haste, ModifierOp.Additive, 0.3, null, null)");
        gen.ShouldContain("new double[] { 4, 8, 12, 16 }");
        gen.ShouldContain("new AttributeModifier(StatAttribute.CooldownRecovery, ModifierOp.AddFinal, null, null, 0.33334)");
        gen.ShouldContain("new CooldownAccelerationChange(2295, 2)");
        gen.ShouldContain("private static readonly int[] Ids = [2141, 2253, 2719, 3216];");
    }

    [Fact]
    public void Emits_RatingCurves_WithOneInstancePerStat()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithEffects);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("public static class RatingCurves");
        gen.ShouldContain("public static RatingCurve Haste { get; } = new(0.16, [(10, 1), (15, 0.98), (20, 0.96), (25, 0.94)], 0.92);");
        gen.ShouldContain("public static RatingCurve CritChance { get; } = new(0.16, [(10, 1)], 0.9);");
    }

    [Fact]
    public void Emits_BothClassesWithEmptyLookups_WhenTheSectionsAreAbsent()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbOf("""{ "id": 1020, "name": "Cold Snap", "icon": "x" }"""));
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("public static class EffectData");
        gen.ShouldContain("private static readonly int[] Ids = [];");
        gen.ShouldContain("public static class RatingCurves");
        gen.ShouldContain("public static RatingCurve Haste { get; } = new(0, [], 0);");
    }

    private const string SpellDbWithTuning = """
        {
          "rime": {
            "ColdSnap": { "id": 1020, "name": "Cold Snap", "icon": "x" }
          },
          "items": {
            "Chronoshift": { "id": 1558, "name": "Chronoshift", "icon": "x" }
          },
          "tuning": {
            "rime": {
              "spells": {
                "ColdSnap": {
                  "A.B": 8,
                  "MaxCharges": 2,
                  "Reduction.Seconds": 1.5,
                  "2ndHit.Damage": 3,
                  "CooldownAcceleration": [0.03, 0.05, 0.08, 0.12],
                  "Repeat.Name": 1,
                  "Repeat_Name": 2,
                  "ColdSnap": 4,
                  "default": 5
                }
              },
              "talents": { "Shatter": { "Chance": 0.25 } },
              "legendaries": { "Frostbound": { "Stacks": 3 } }
            },
            "items": { "spells": { "Chronoshift": { "Channel.WhileActiveAddedCooldownRecovery": 8 } } },
            "glyphs": { "glyphs": { "TheMonarch": { "HasteThreshold": [0.5, 0.5, 0.5, 0.5] } } }
          }
        }
        """;

    [Fact]
    public void Emits_ANumberTuningRow_AsAConstantInTheMembersNestedClass()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("namespace FellowshipAnalyzer.Core.Common.Spells.Rime;");
        gen.ShouldContain("public static class SpellTuning");
        gen.ShouldContain("public static class ColdSnap");
        gen.ShouldContain("public const double A_B = 8;");
        gen.ShouldContain("public const double MaxCharges = 2;");
        gen.ShouldContain("public const double Reduction_Seconds = 1.5;");
    }

    [Fact]
    public void Emits_AnArrayTuningRow_AsAReadOnlySpanProperty()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("public static global::System.ReadOnlySpan<double> CooldownAcceleration => [0.03, 0.05, 0.08, 0.12];");
    }

    [Fact]
    public void Emits_ARowKeyStartingWithADigit_WithALeadingUnderscore()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();

        run.ConcatenatedGenerated.ShouldContain("public const double _2ndHit_Damage = 3;");
    }

    [Fact]
    public void Omits_RowsSharingAnIdentifier_AndARowNamedForItsMember()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning).ConcatenatedGenerated;

        gen.ShouldNotContain("Repeat_Name");
        gen.ShouldNotContain("const double ColdSnap =");
    }

    [Fact]
    public void Escapes_ARowIdentifierThatIsAKeyword()
    {
        var gen = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning).ConcatenatedGenerated;

        gen.ShouldContain("public const double @default = 5;");
    }

    [Fact]
    public void Emits_TalentLegendaryItemsAndGlyphTuning_InTheirOwnClassesAndNamespaces()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbWithTuning);
        run.CompilationDiagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
        var gen = run.ConcatenatedGenerated;

        gen.ShouldContain("public static class TalentTuning");
        gen.ShouldContain("public const double Chance = 0.25;");
        gen.ShouldContain("public static class LegendaryTuning");
        gen.ShouldContain("public const double Stacks = 3;");
        gen.ShouldContain("namespace FellowshipAnalyzer.Core.Common.Items;");
        gen.ShouldContain("public const double Channel_WhileActiveAddedCooldownRecovery = 8;");
        gen.ShouldContain("namespace FellowshipAnalyzer.Core.Game;");
        gen.ShouldContain("public static class GlyphTuning");
        gen.ShouldContain("public static global::System.ReadOnlySpan<double> HasteThreshold => [0.5, 0.5, 0.5, 0.5];");
    }

    [Fact]
    public void Emits_NoTuningClass_AndReportsNoDiagnostic_WhenTheSectionIsAbsent()
    {
        var run = SpellDatabaseGeneratorTestHarness.Run(SpellDbOf("""{ "id": 1020, "name": "Cold Snap", "icon": "x" }"""));

        run.ConcatenatedGenerated.ShouldNotContain("Tuning");
        run.DriverDiagnostics.ShouldBeEmpty();
    }
}
