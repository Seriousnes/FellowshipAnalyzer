using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Generators.Tests;

public class RegistryGeneratorObjectInitializerTests
{
    private const string Source = """
        using System;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class GenerateRegistryAttribute<T> : Attribute where T : class { }

        namespace FellowshipAnalyzer.Core.Common.Spells
        {
            public interface ISpellRegistry { }
            public record Spell(int Id = 0, string Name = "", string Icon = "")
            {
                public virtual int Guid => Id;
            }
            public record Effect(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
            {
                public override int Guid => 1_000_000 + Id;
            }

            [GenerateRegistry<ISpellRegistry>]
            public static partial class Spells
            {
                public static Spell Chronoshift { get; } = new() { Id = 1558 };
                public static Effect Kindling { get; } = new() { Id = 104 };
            }
        }
        """;

    [Fact]
    public void EmitsGuids_ForObjectInitializerMembers()
    {
        var result = RegistryGeneratorTestHarness.Run(Source);
        result.DriverDiagnostics.ShouldBeEmpty();
        var gen = result.ConcatenatedGenerated;
        gen.ShouldContain("public const int Chronoshift = 1558;");
        gen.ShouldContain("public const int Kindling = 1000104;");
        gen.ShouldContain("Spells.Chronoshift.Guid");
        gen.ShouldContain("Spells.Kindling.Guid");
    }
}
