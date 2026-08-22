namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// The game's ability-category taxonomy, as the game-data export's <c>settings.json</c>
/// <c>abilityCategories</c> block declares it, minus <c>None</c> which is no category. This is the
/// in-game classification of an ability, distinct from <see cref="Analysis.SpellCategory"/>, which is the
/// tool's internal analysis grouping (rotational, cooldowns, defensive, and so on).
/// </summary>
public enum AbilityCategory
{
    /// <summary>
    /// A hero's default, always-available attack.
    /// </summary>
    Basic,
    /// <summary>
    /// One of a hero's core rotational abilities.
    /// </summary>
    Core,
    /// <summary>
    /// A hero's major cooldown ability.
    /// </summary>
    Major,
    /// <summary>
    /// An ability that generates or spends the hero's power resource.
    /// </summary>
    Power,
    /// <summary>
    /// An ability that generates or spends Spirit.
    /// </summary>
    Spirit,
    /// <summary>
    /// An ability whose primary purpose is crowd control (stuns, roots, and similar effects).
    /// </summary>
    Control,
    /// <summary>
    /// An ability used to mitigate or avoid incoming damage.
    /// </summary>
    Defensive,
    /// <summary>
    /// A support ability that does not fit the other combat categories.
    /// </summary>
    Utility,
    /// <summary>
    /// An ability whose primary purpose is repositioning.
    /// </summary>
    Movement,
    /// <summary>
    /// An ability granted by the hero's equipped weapon.
    /// </summary>
    Weapon,
    /// <summary>
    /// An ability granted by an equipped relic, shared across every hero that can equip it.
    /// </summary>
    Relic,
}
