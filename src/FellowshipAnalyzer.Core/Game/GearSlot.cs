namespace FellowshipAnalyzer.Core.Game;

/// <summary>The equipment slots a combatant's gear can occupy, as reported by <c>combatantInfo</c>.</summary>
public enum GearSlot
{
    /// <summary>The head armor slot.</summary>
    Head = 0,

    /// <summary>The necklace slot.</summary>
    Necklace = 1,

    /// <summary>The shoulders armor slot.</summary>
    Shoulders = 2,

    /// <summary>The back/cloak slot.</summary>
    Back = 3,

    /// <summary>The chest armor slot.</summary>
    Chest = 4,

    /// <summary>The wrists armor slot.</summary>
    Wrists = 5,

    /// <summary>The hands armor slot.</summary>
    Hands = 6,

    /// <summary>The legs armor slot.</summary>
    Legs = 7,

    /// <summary>The feet armor slot.</summary>
    Feet = 8,

    /// <summary>The first of the two ring slots.</summary>
    Ring1 = 9,

    /// <summary>The second of the two ring slots.</summary>
    Ring2 = 10,

    /// <summary>The first of the two relic slots.</summary>
    Relic1 = 11,

    /// <summary>The second of the two relic slots.</summary>
    Relic2 = 12,

    /// <summary>The equipped weapon slot.</summary>
    Weapon = 13,
}
