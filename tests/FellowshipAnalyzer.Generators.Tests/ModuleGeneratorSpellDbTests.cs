using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class ModuleGeneratorSpellDbTests
{
    private const string SpellDb = """
        {
          "shared": {
            "Fortitude": { "id": 1700, "name": "Fortitude" }
          },
          "items": {
            "Chronoshift": { "kind": "ability", "id": 1558, "name": "Chronoshift", "cooldown": 180 },
            "ChronoshiftBuff": { "kind": "effect", "id": 2279, "name": "Asha's Arcane Embrace" }
          }
        }
        """;

    private static string Source(string spellArgument) => $$"""
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        using FellowshipAnalyzer.Core.Common.Items;
        using FellowshipAnalyzer.Core.Common.Spells;

        [System.AttributeUsage(System.AttributeTargets.Class)]
        public sealed class GenerateRegistryAttribute<T> : System.Attribute where T : class { }

        namespace FellowshipAnalyzer.Core.Common.Spells
        {
            public interface ISpellRegistry { }
            public record Spell(int Id = 0, string Name = "", string Icon = "");

            [GenerateRegistry<ISpellRegistry>]
            public static partial class Spells { }
        }

        namespace FellowshipAnalyzer.Core.Common.Items
        {
            public interface IItemRegistry { }
            public partial class Items : IItemRegistry { }
        }

        namespace FellowshipAnalyzer.Core.Analysis
        {
            public partial class Probe : Analyzer
            {
                [On<DamageEvent>(Spell = {{spellArgument}})]
                private void OnDamage(DamageEvent e) { }
            }
        }
        """;

    [Fact]
    public void ResolvesGeneratedItemsMember_FromSpellDb()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(Items.Chronoshift)"), SpellDb);
        result.DriverDiagnostics.ShouldBeEmpty();
        result.ConcatenatedGenerated.ShouldContain(".Ability.Id == 1558");
    }

    [Fact]
    public void ResolvesGeneratedItemsEffect_WithEffectOffset()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(Items.ChronoshiftBuff)"), SpellDb);
        result.DriverDiagnostics.ShouldBeEmpty();
        result.ConcatenatedGenerated.ShouldContain(".Ability.Id == 1002279");
    }

    [Fact]
    public void ResolvesGeneratedSharedMember_OnTheCentralRegistry()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(Spells.Fortitude)"), SpellDb);
        result.DriverDiagnostics.ShouldBeEmpty();
        result.ConcatenatedGenerated.ShouldContain(".Ability.Id == 1700");
    }

    [Fact]
    public void ResolvesFullyQualifiedGeneratedItemsMember()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(FellowshipAnalyzer.Core.Common.Items.Items.Chronoshift)"), SpellDb);
        result.DriverDiagnostics.ShouldBeEmpty();
        result.ConcatenatedGenerated.ShouldContain(".Ability.Id == 1558");
    }

    [Fact]
    public void ReportsFA0003_ForAMemberSpellDbDoesNotGenerate()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(Items.Missing)"), SpellDb);
        result.DriverDiagnostics.ShouldContain(d => d.Id == "FA0003");
        result.ConcatenatedGenerated.ShouldNotContain(".Ability.Id ==");
    }

    [Fact]
    public void ReportsFA0003_WithoutSpellDb()
    {
        var result = GeneratorTestHarness.Run(Source("nameof(Items.Chronoshift)"));
        result.DriverDiagnostics.ShouldContain(d => d.Id == "FA0003");
    }
}
