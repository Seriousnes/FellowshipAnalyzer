namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// A single pull within a dungeon-type <see cref="ReportFight"/>, as reported by Fellowship Logs.
/// An <see cref="EncounterId"/> of 0 denotes a trash pull; any other value denotes a boss encounter.
/// </summary>
public sealed record DungeonPull(
    int Id,
    int EncounterId,
    bool? Kill,
    double StartTime,
    double EndTime,
    string Name,
    IReadOnlyList<DungeonPullNpc>? EnemyNpcs
);
