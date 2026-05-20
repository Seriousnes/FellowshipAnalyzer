namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents a single fight/encounter within a report.
/// </summary>
public sealed record ReportFight(
    int Id,
    string Name,
    int EncounterId,
    bool? Kill,
    double StartTime,
    double EndTime,
    int? Difficulty,
    IReadOnlyList<int>? FriendlyPlayers,
    double? FightPercentage,
    bool InProgress = false
);