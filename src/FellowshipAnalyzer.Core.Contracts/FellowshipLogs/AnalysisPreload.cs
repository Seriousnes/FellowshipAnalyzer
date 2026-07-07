namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Combined preload data for analysis: report info and master data fetched in a single API call.
/// </summary>
public sealed record AnalysisPreload(
    ReportInfo ReportInfo,
    ReportMasterData MasterData
);
