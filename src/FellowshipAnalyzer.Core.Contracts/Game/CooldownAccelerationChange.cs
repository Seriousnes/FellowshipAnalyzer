namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// The Cooldown Acceleration an effect gives one ability.
/// </summary>
/// <param name="AbilityId">Native ability id.</param>
/// <param name="Value">Cooldown Acceleration value.</param>
public readonly record struct CooldownAccelerationChange(int AbilityId, double Value);
