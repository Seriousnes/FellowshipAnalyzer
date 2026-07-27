namespace FellowshipAnalyzer.Core.Game;

/// <summary>Identifies a resource pool a hero's kit can generate or spend, keying the per-type state kept by <see cref="Resources.ResourceTracker"/>.</summary>
public enum ResourceTypes
{
    /// <summary>Mana-based resource pool, spent on casts by heroes whose kit is mana-costed rather than built around a bespoke primary resource.</summary>
    [ResourceName("Mana")]
    Mana = 1,
    /// <summary>
    /// Common primary resource for heroes, e.g. Mara's Energy, Rime's Anima, etc.
    /// </summary>
    [ResourceName("Anima", "Energy", "Fury", "Chrona", "Cinders", "Focus", "Radiant Runes")]
    Primary = 2,
    /// <summary>
    /// Common secondary resource for heroes, e.g. Mara's Combo Points, etc.
    /// </summary>
    [ResourceName("Combo Points", "Toughness")]
    Secondary = 3,
    /// <summary>The universal Spirit resource that charges Spirit of Heroism, generated and spent by every hero regardless of class.</summary>
    [ResourceName("Spirit")]
    Spirit = 4,
    /// <summary>
    /// Tertiary resources, e.g. Rime's Winter Orbs, etc.
    /// </summary>
    [ResourceName("Winter Orbs", "Blood Feathers", "Pink Butterflies")]
    Tertiary = 5,
    /// <summary>
    /// Delayed HP damage resource when Aeona is in the party.
    /// </summary>
    Stagger = 7,
}
