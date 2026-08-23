namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// A character's recent reports as returned by the character API endpoint.
/// </summary>
public sealed record CharacterReports(
    string Name,
    List<ReportSummary> Reports
);
