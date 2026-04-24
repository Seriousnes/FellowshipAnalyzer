using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

public sealed record FellowshipLogsEventsRequest(
    string ReportCode,
    int PlayerId,
    int FightId
);

/// <summary>
/// The result of fetching events for a fight, including whether the fight was still in progress
/// at the time of the request. In-progress fights should not be cached.
/// </summary>
public sealed record EventsResult(
    IReadOnlyList<Event> Events,
    bool InProgress
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
    Task<ReportInfo> GetAsync(
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
    Task<ReportMasterData> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default);
}

public interface IAnalysisPreloadFunction
{
    Task<AnalysisPreload> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default);
}
