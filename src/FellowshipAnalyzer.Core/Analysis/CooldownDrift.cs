namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A cast the log has for an ability whose <see cref="CooldownState"/> had no charge. The state returned the
/// charge recharging at the cast's timestamp.
/// </summary>
/// <param name="SpellId">Primary spell id of the ability.</param>
/// <param name="Timestamp">Real millisecond of the cast.</param>
/// <param name="Milliseconds">Real milliseconds by which the state was late: the time left until the charge recharging returned.</param>
public readonly record struct CooldownDrift(int SpellId, int Timestamp, int Milliseconds);
