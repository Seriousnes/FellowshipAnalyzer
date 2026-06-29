using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FellowshipAnalyzer.Generators.Tests;

internal static class RegistryGeneratorTestHarness
{
    public static GeneratorRunResult Run(string userSource)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var userTree = CSharpSyntaxTree.ParseText(userSource, parseOptions);

        var trustedAssembliesPaths =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator);

        var refList = trustedAssembliesPaths
            .Where(p => File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests.Compilation",
            syntaxTrees: [userTree],
            references: refList,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(
                generators: [new RegistryGenerator().AsSourceGenerator()],
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
