using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>What guide insights need from a hero's parser.</summary>
/// <param name="Guides">Guide component of each playbook guide, keyed by guide name, in guide order.</param>
/// <param name="Spells">Abilities and talents whose names become spell links in insight text.</param>
/// <param name="Messages">UTF-8 JSON user message of each guide with a row to read, keyed by guide name.</param>
public sealed record HeroInsights(
    IReadOnlyDictionary<string, Type> Guides,
    IReadOnlyList<Spell> Spells,
    Func<IReadOnlyDictionary<string, byte[]>> Messages);
