namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Marks a concrete CombatLogParser as a hero analyzer with a specific hero ID.
/// The source generator uses this to register the parser as a keyed service.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HeroAnalyzerAttribute(string heroId) : Attribute
{
    public string HeroId { get; } = heroId;
}
