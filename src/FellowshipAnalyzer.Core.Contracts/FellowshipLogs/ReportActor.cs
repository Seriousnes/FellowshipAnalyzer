namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents a player, NPC, or pet actor in a report.
/// </summary>
public sealed record ReportActor(
    int Id,
    string Name,
    string Type,
    string? SubType,
    string? Server,
    string? Icon
);
