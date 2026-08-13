namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// A brief summary of a report used in character report listings.
/// </summary>
public sealed record ReportSummary(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    int DungeonCount
);
