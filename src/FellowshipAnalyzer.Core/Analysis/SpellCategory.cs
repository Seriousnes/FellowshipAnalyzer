namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The tool's internal grouping for a spellbook ability, distinct from the game's own
/// <see cref="Common.Spells.AbilityCategory"/> taxonomy.
/// </summary>
public enum SpellCategory
{
    /// <summary>No grouping has been assigned yet; a hero's spellbook is expected to give every ability a real category.</summary>
    Uncategorized,

    /// <summary>A core single-target rotational ability.</summary>
    Rotational,

    /// <summary>A core rotational ability used against multiple targets.</summary>
    RotationalAoe,

    /// <summary>A longer-recharge ability used for burst or major effects.</summary>
    Cooldowns,

    /// <summary>An ability that mitigates or avoids incoming damage.</summary>
    Defensive,

    /// <summary>A support ability such as an interrupt or a taunt.</summary>
    Utility,

    /// <summary>A consumable-item ability, such as a potion, rather than a hero ability.</summary>
    Consumable,

    /// <summary>An ability excluded from the spellbook's category groupings, such as a hero's basic attack.</summary>
    Hidden,
}
