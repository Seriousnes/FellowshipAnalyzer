namespace FellowshipAnalyzer.Core.Game;

public enum ResourceTypes
{
    Mana = 1,
    /// <summary>
    /// Common primary resource for hereos, e.g. Mara's Energy, Rime's Anima, etc
    /// </summary>
    Primary = 2,
    /// <summary>
    /// Common secondary resource for heroes, e.g. Mara's Combo Points, etc
    /// </summary>
    Secondary = 3,
    Spirit = 4,
    /// <summary>
    /// Tertiary Resources,e.g. Rime's Winter Orb, etc
    /// </summary>
    Tertiary = 5,
    /// <summary>
    /// Delayed HP damage resource when Aeona is in the party
    /// </summary>
    Stagger = 7,
}
