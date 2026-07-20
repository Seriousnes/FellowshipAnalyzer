namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// The game's Season 3 ability-category taxonomy as it appears in <c>hero_data.json</c>. This is the
/// in-game classification of an ability, distinct from <see cref="Analysis.SpellCategory"/>, which is the
/// tool's internal analysis grouping (rotational, cooldowns, defensive, and so on).
/// </summary>
public enum AbilityCategory
{
    Basic,
    Control,
    Core,
    Defensive,
    Major,
    Movement,
    Power,
    Utility,
    Spirit,
}
