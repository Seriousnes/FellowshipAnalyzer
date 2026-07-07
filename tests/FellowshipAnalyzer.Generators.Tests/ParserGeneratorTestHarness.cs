using System.Collections.Immutable;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Generators.Tests;

/// <summary>
/// Runs <see cref="CombatLogParserGenerator"/> (alongside <see cref="ModuleGenerator"/>) against a
/// compilation that references the real <c>FellowshipAnalyzer.Core</c> assembly, so generated
/// parser surface is checked against the real attribute, <c>Pull</c>, and result-model types rather
/// than a hand-maintained preamble.
/// </summary>
internal static class ParserGeneratorTestHarness
{
    private static readonly string[] AnchorAssemblyLocations =
    [
        typeof(object).Assembly.Location,
        typeof(CombatLogParser).Assembly.Location,
        typeof(ReportFight).Assembly.Location,
        typeof(IServiceCollection).Assembly.Location,
        typeof(ServiceCollection).Assembly.Location,
    ];

    public static GeneratorRunResult Run(string userSource)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var userTree = CSharpSyntaxTree.ParseText(userSource, parseOptions);

        var trustedAssembliesPaths =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator);

        var refPaths = new HashSet<string>(trustedAssembliesPaths.Where(File.Exists));
        foreach (var location in AnchorAssemblyLocations)
            if (File.Exists(location)) refPaths.Add(location);

        var refList = refPaths
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "ParserGeneratorTests.Compilation",
            syntaxTrees: [userTree],
            references: refList,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(
                generators:
                [
                    new ModuleGenerator().AsSourceGenerator(),
                    new CombatLogParserGenerator().AsSourceGenerator(),
                ],
                additionalTexts: default,
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
}
