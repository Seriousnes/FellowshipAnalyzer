namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Marks a concrete CombatLogParser as the analyzer for a specific <see cref="HeroName"/>.
/// The source generator uses this to register the parser as a keyed service
/// using the hero's lowercase string identifier (see <see cref="HeroNameExtensions.ToHeroId"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HeroAnalyzerAttribute(HeroName hero) : Attribute
{
    public HeroName Hero { get; } = hero;
}
