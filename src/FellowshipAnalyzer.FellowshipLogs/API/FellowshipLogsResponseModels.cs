using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class FellowshipLogsResponse<T>
{
    public T Data { get; set; } = default!;
}

internal sealed class FellowshipLogsReportResponse
{
    public FellowshipLogsReportData ReportData { get; set; } = default!;

    /// <summary>
    /// Root-level fights from the events query (contains inProgress status).
    /// Not present in report queries.
    /// </summary>
    public List<FellowshipLogsEventFightStatus>? Fights { get; set; }
}

/// <summary>
/// Minimal fight metadata returned at the root level of the events GraphQL query.
/// </summary>
internal sealed class FellowshipLogsEventFightStatus
{
    public bool InProgress { get; set; }
}

internal sealed class FellowshipLogsReportData
{
    public FellowshipLogsReport Report { get; set; } = default!;
}

internal sealed class FellowshipLogsReport
{
    public FellowshipLogsEventsData? Events { get; set; }
    public string? Title { get; set; }
    public double StartTime { get; set; }
    public double? EndTime { get; set; }
    public List<FellowshipLogsReportFight>? Fights { get; set; }
    public FellowshipLogsReportMasterData? MasterData { get; set; }
}

internal sealed class FellowshipLogsEventsData
{
    public List<Event> Data { get; set; } = [];
}

internal sealed class FellowshipLogsReportFight
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int EncounterID { get; set; }
    public bool? Kill { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int? Difficulty { get; set; }
    public List<int>? FriendlyPlayers { get; set; }
    public bool InProgress { get; set; }
}

internal sealed class FellowshipLogsReportMasterData
{
    public List<FellowshipLogsReportActor>? Actors { get; set; }
}

internal sealed class FellowshipLogsReportActor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? SubType { get; set; }
    public string? Server { get; set; }
}
