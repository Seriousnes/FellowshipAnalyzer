using System.Collections.Immutable;

using FellowshipAnalyzer.Core.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers.Tests;

/// <summary>
/// Runs <see cref="PullAnalyzerDiagnostics"/> over a compilation that references the real
/// <c>FellowshipAnalyzer.Core</c> assembly, so crafted parsers use the real <c>[AddState]</c> /
/// <c>[AddAnalyzer]</c> / <c>[ForPull]</c> / <c>Analyzer&lt;T&gt;</c> surface.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly string[] AnchorAssemblyLocations =
    [
        typeof(object).Assembly.Location,
        typeof(CombatLogParser).Assembly.Location,
    ];

    public static ImmutableArray<Diagnostic> Run(string userSource)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(userSource, parseOptions);

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
            assemblyName: "AnalyzerTests.Compilation",
            syntaxTrees: [tree],
            references: refList,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var withAnalyzers = compilation.WithAnalyzers([new PullAnalyzerDiagnostics()]);
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
