using System.Collections.Immutable;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipAnalyzer.Generators.Tests;

internal static class SpellDatabaseGeneratorTestHarness
{
    private const string Preamble = """
        using System;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class GenerateRegistryAttribute<T> : Attribute where T : class { }

        namespace FellowshipAnalyzer.Core.Common.Spells
        {
            public interface ISpellRegistry { }

            public record Spell(int Id = 0, string Name = "", string Icon = "")
            {
                public virtual int Guid => Id;
                public double? Cooldown { get; init; }
                public int? Range { get; init; }
                public int Charges { get; init; } = 1;
                public double? CastDuration { get; init; }
                public double? ChannelDuration { get; init; }
                public double? ChannelTickInterval { get; init; }
                public virtual int? SpiritCost { get; init; }
                public virtual int? WinterOrbCost { get; init; }
                public virtual int? AnimaCost { get; init; }
                public int? FocusCost { get; init; }
            }

            public record Effect(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
            {
                public override int Guid => 1_000_000 + Id;
            }

            public record Talent(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
            {
                public override int Guid => 2_000_000 + Id;
            }

            public record Weapon(int Id = 0, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
            {
                public override int Guid => 3_000_000 + Id;
            }

            [GenerateRegistry<ISpellRegistry>]
            public static partial class Spells
            {
                public static Spell EpochBreak { get; } = new() { Id = 1881, Name = "Epoch Break" };
                public static Effect EpochBreakBuff { get; } = new() { Id = 2613, Name = "Epoch Break" };
            }
        }
        """;

    public static GeneratorRunResult Run(string spellDbJson)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var preambleTree = CSharpSyntaxTree.ParseText(Preamble, parseOptions);

        var trustedAssembliesPaths =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator);

        var refList = trustedAssembliesPaths
            .Where(p => File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "SpellDatabaseGeneratorTests.Compilation",
            syntaxTrees: [preambleTree],
            references: refList,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var additionalText = new InMemoryAdditionalText("data/spelldb.json", spellDbJson);

        var driver = CSharpGeneratorDriver
            .Create(
                generators: [new ConsolidatedSpellRegistryGenerator().AsSourceGenerator()],
                additionalTexts: [additionalText],
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var driverResult = driver.GetRunResult();

        var generatedTexts = driverResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToImmutableArray();

        var compilationDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToImmutableArray();

        return new GeneratorRunResult(generatedTexts, driverDiagnostics, compilationDiagnostics);
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(content, Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
