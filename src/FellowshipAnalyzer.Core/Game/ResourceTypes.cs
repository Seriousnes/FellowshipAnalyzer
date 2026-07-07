namespace FellowshipAnalyzer.Core.Game;

public enum ResourceTypes
{
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
