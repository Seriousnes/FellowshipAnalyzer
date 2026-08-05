using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class ModuleGeneratorObjectInitializerTests
{
    private const string Source = """
        using FellowshipAnalyzer.Core.Analysis;
        using FellowshipAnalyzer.Core.Events;
        using FellowshipAnalyzer.Core.Common.Spells;

        namespace FellowshipAnalyzer.Core.Common.Spells
        {
            public interface ISpellRegistry { }
            public record Spell(int Id = 0, string Name = "", string Icon = "")
            {
                public int? Cooldown { get; init; }
            }
            public record Effect(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
            {
            }
            public partial class Rime : ISpellRegistry
            {
                public static Spell FreezingTorrent { get; } = new() { Id = 1027, Cooldown = 15 };
                public static Effect BurstingIceDamage { get; } = new() { Id = 1396 };
            }
        }

        namespace Sample
        {
            public partial class Probe : Analyzer
            {
                [On<DamageEvent>(Spell = nameof(Rime.FreezingTorrent))]
                private void OnChannel(DamageEvent e) { }

                [On<DamageEvent>(Spell = nameof(Rime.BurstingIceDamage))]
                private void OnEffect(DamageEvent e) { }
            }
        }
        """;

    [Fact]
    public void ResolvesGuid_FromObjectInitializerId()
    {
        var result = GeneratorTestHarness.Run(Source);
        result.DriverDiagnostics.ShouldBeEmpty();
        var gen = result.ConcatenatedGenerated;
        gen.ShouldContain(".Ability.Id == 1027");
        gen.ShouldContain(".Ability.Id == 1001396");
    }
}
