using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Top-level GraphQL response envelope.
/// </summary>
public sealed class GraphQLResponse<T>
{
    public T Data { get; set; } = default!;
}

/// <summary>
/// Root data object for both report and events GraphQL queries.
/// </summary>
public sealed class GraphQLReportResponse
{
    public GraphQLReportData ReportData { get; set; } = default!;

    /// <summary>
    /// Root-level fights from the events query (contains inProgress status).
    /// Not present in report queries.
    /// </summary>
    public List<GraphQLEventFightStatus>? Fights { get; set; }
}

/// <summary>
/// Minimal fight metadata returned at the root level of the events GraphQL query.
/// </summary>
public sealed class GraphQLEventFightStatus
{
    public bool InProgress { get; set; }
}

public sealed class GraphQLReportData
{
    public GraphQLReport Report { get; set; } = default!;
}

public sealed class GraphQLReport
{
    public GraphQLEventsData? Events { get; set; }
    public string? Title { get; set; }
    public double StartTime { get; set; }
    public double? EndTime { get; set; }
    public List<GraphQLReportFight>? Fights { get; set; }
    public GraphQLReportMasterData? MasterData { get; set; }
}

public sealed class GraphQLEventsData
{
    public List<Event> Data { get; set; } = [];
}

public sealed class GraphQLReportFight
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int EncounterID { get; set; }
    public bool? Kill { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int? Difficulty { get; set; }
    public List<int>? FriendlyPlayers { get; set; }
    public double? FightPercentage { get; set; }
    public bool InProgress { get; set; }
}

public sealed class GraphQLReportMasterData
{
    public List<GraphQLReportAbility>? Abilities { get; set; }
    public List<GraphQLReportActor>? Actors { get; set; }
}

public sealed class GraphQLReportAbility
{
    public int GameID { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class GraphQLReportActor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? SubType { get; set; }
    public string? Server { get; set; }
    public string? Icon { get; set; }
}
