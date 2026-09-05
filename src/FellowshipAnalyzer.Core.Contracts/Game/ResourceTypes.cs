namespace FellowshipAnalyzer.Core.Game;

/// <summary>Identifies a resource pool a hero's kit generates or spends.</summary>
public enum ResourceTypes
{
    /// <summary>Mana resource pool.</summary>
    [ResourceName("Mana")]
    Mana = 1,
    /// <summary>Common primary resource for heroes.</summary>
    [ResourceName("Anima", "Energy", "Fury", "Chrona", "Cinders", "Focus", "Radiant Runes", "Radiant Rune")]
    Primary = 2,
    /// <summary>Common secondary resource for heroes.</summary>
    [ResourceName("Combo Points", "Toughness")]
    Secondary = 3,
    /// <summary>The universal Spirit resource.</summary>
    [ResourceName("Spirit")]
    Spirit = 4,
    /// <summary>Tertiary resources.</summary>
    [ResourceName("Winter Orbs", "Blood Feathers", "Pink Flutterflies")]
    Tertiary = 5,
    /// <summary>The Stagger resource.</summary>
    Stagger = 7,
}
