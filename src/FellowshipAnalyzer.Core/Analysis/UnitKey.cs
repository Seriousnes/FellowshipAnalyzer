namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Identifies a distinct combat unit by its report-global actor id and per-actor spawn instance.
/// A null <see cref="Instance"/> denotes a unit with no instance index (players and single-spawn
/// actors); two spawns that share one actor id occupy distinct keys.
/// </summary>
public readonly record struct UnitKey(int ActorId, int? Instance);
