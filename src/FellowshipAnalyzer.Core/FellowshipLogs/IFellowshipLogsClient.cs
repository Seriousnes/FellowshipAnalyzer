using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

public sealed record FellowshipLogsEventsRequest(
    string ReportCode,
    int PlayerId,
    int FightId
);

/// <summary>
/// Represents a single ability entry from report master data.
/// </summary>
public sealed record FellowshipLogsAbility(
    int GameId,
    string Name,
    string Icon,
    string Type
);

/// <summary>
/// Master data for a report: abilities and player actors, cached at the report level.
/// </summary>
public sealed record FellowshipLogsMasterData(
    IReadOnlyList<FellowshipLogsAbility> Abilities,
    IReadOnlyList<FellowshipLogsActor> Actors
);

/// <summary>
/// Combined preload data for analysis: report info and master data fetched in a single API call.
/// </summary>
public sealed record FellowshipLogsAnalysisPreload(
    FellowshipLogsReportInfo ReportInfo,
    FellowshipLogsMasterData MasterData
);

/// <summary>
/// Represents metadata about a combat log report, including fights and actors.
/// </summary>
public sealed record FellowshipLogsReportInfo(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    IReadOnlyList<FellowshipLogsFight> Fights,
    IReadOnlyList<FellowshipLogsActor> Actors
);

/// <summary>
/// Represents a single fight/encounter within a report.
/// </summary>
public sealed record FellowshipLogsFight(
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

/// <summary>
/// The result of fetching events for a fight, including whether the fight was still in progress
/// at the time of the request. In-progress fights should not be cached.
/// </summary>
public sealed record EventsResult(
    IReadOnlyList<Event> Events,
    bool InProgress
);

/// <summary>
/// Represents a player, NPC, or pet actor in a report.
/// </summary>
public sealed record FellowshipLogsActor(
    int Id,
    string Name,
    string Type,
    string? SubType,
    string? Server,
    string? Icon
);

public interface IFellowshipLogsClient
{
    IReportFunction Report { get; }
    IEventsFunction Events { get; }
    IMasterDataFunction MasterData { get; }
    IAnalysisPreloadFunction AnalysisPreload { get; }
}

public interface IReportFunction
{
    Task<FellowshipLogsReportInfo> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default);
}

public interface IEventsFunction
{
    Task<EventsResult> GetAsync(
        FellowshipLogsEventsRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMasterDataFunction
{
    Task<FellowshipLogsMasterData> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default);
}

public interface IAnalysisPreloadFunction
{
    Task<FellowshipLogsAnalysisPreload> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default);
}
