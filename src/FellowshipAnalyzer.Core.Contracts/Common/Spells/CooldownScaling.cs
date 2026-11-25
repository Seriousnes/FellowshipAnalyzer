using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// The attributes an ability's cooldown scales off, as the game-data export lists them in
/// <c>cooldownDurationScalesOff</c> and <c>cooldownTimeRateScalesOff</c>. The duration lists change the cooldown's
/// length when it starts. The time-rate lists change how fast it recovers while it runs. The serialised form
/// states only the flags that are <c>true</c>.
/// </summary>
public sealed record CooldownScaling
{
    /// <summary>Ability Cooldown Reduction shortens the cooldown's duration.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DurationCooldownReduction { get; init; }

    /// <summary>Haste shortens the cooldown's duration.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DurationHaste { get; init; }

    /// <summary>Cooldown Recovery adds to the cooldown's time rate.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TimeRateCooldownRecovery { get; init; }

    /// <summary>Haste adds to the cooldown's time rate.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TimeRateHaste { get; init; }
}
