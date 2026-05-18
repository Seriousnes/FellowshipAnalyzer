namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Marker for <c>HeroManifestGenerator</c>. Applied to any class in the consuming project,
/// the generator emits a <c>HeroManifest</c> with one entry per <c>[HeroAnalyzer]</c> class
/// discovered across referenced assemblies, plus an <c>AddFellowshipHeroAnalysis</c> DI extension
/// that wires each hero's generated <c>Add{Hero}Analysis()</c> registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateHeroManifestAttribute : Attribute { }
