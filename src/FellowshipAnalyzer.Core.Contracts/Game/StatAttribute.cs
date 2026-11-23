namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// An attribute an effect's modifier changes and the Haste and cooldown model reads. The member names
/// are the game-data export's <c>attribute</c> strings.
/// </summary>
public enum StatAttribute
{
    /// <summary>Haste, written as a fraction: 0.30 for 30%.</summary>
    Haste,
    /// <summary>Haste rating.</summary>
    HasteRating,
    /// <summary>Cooldown Recovery, which speeds cooldown progress.</summary>
    CooldownRecovery,
    /// <summary>Ability Cooldown Reduction, which shortens cooldown duration.</summary>
    CooldownReduction,
}
