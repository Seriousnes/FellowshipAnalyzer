namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>Where Haste enters an ability's cooldown, as the game-data export states it.</summary>
public enum CooldownHasteScaling
{
    /// <summary>
    /// The cooldown's duration is divided by <c>1 + haste</c> once, when the cooldown starts. The export
    /// lists Haste in <c>cooldownDurationScalesOff</c>.
    /// </summary>
    Duration,

    /// <summary>
    /// Haste adds to the cooldown's time rate beside Cooldown Acceleration for as long as it runs. The export
    /// lists Haste in <c>cooldownTimeRateScalesOff</c>.
    /// </summary>
    TimeRate,
}
