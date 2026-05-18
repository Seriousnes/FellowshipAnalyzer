using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer;

/// <summary>
/// §6 of the redesign doc: the marker that triggers <c>HeroManifestGenerator</c> to scan all
/// referenced hero assemblies for <c>[HeroAnalyzer]</c> types and emit:
/// <list type="bullet">
///   <item><c>HeroManifestEntry</c> record + <c>HeroManifest.Entries</c> static list</item>
///   <item><c>AddFellowshipHeroAnalysis</c> extension method that calls each hero's generated <c>Add{Hero}Analysis()</c></item>
/// </list>
/// Replaces the eleven-line DI chain that previously lived here.
/// </summary>
[GenerateHeroManifest]
internal static partial class HeroAnalysisHost { }
