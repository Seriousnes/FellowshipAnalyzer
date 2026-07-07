namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Metadata about a combat log report: title, timing, fights, and actors.
/// </summary>
public sealed record ReportInfo(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    IReadOnlyList<ReportFight> Fights,
    IReadOnlyList<ReportActor> Actors
);
