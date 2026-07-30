namespace FellowshipAnalyzer.Core.Common;

/// <summary>The kind of actor an event's source or target is.</summary>
public enum UnitTypeEnum
{
    /// <summary>A player character.</summary>
    Player,
    /// <summary>A non-player enemy.</summary>
    NPC,
    /// <summary>A pet or summoned unit belonging to a player.</summary>
    Pet
}
