using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer;

/// <summary>
/// Marker that triggers <c>HeroManifestGenerator</c> to scan all referenced hero assemblies
/// for <c>[HeroAnalyzer]</c> types and emit:
/// <list type="bullet">
///   <item><c>HeroManifestEntry</c> record + <c>HeroManifest.Entries</c> static list</item>
///   <item><c>AddFellowshipHeroAnalysis</c> extension method that calls each hero's generated <c>Add{Hero}Analysis()</c></item>
/// </list>
/// </summary>
[GenerateHeroManifest]
internal static partial class HeroAnalysisHost { }
