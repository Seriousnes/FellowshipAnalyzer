using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

public sealed record FellowshipLogsEventsRequest(
    string ReportCode,
    int PlayerId,
    int FightId
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
    string? Server
);

public interface IFellowshipLogsClient
{
    IReportFunction Report { get; }
    IEventsFunction Events { get; }
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
