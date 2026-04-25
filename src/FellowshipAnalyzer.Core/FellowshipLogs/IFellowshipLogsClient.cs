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

    /// <summary>
    /// Fetches the raw UTF-8 JSON body of an <see cref="EventsResult"/> response without deserializing it.
    /// Used by the WASM client to separately measure network I/O vs JSON deserialization,
    /// and to cache the original response bytes (avoiding a costly re-serialize on the cache-write path).
    /// Server-side implementations may throw <see cref="NotSupportedException"/>.
    /// </summary>
    Task<RawEventsResponse> GetRawBytesAsync(
        FellowshipLogsEventsRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raw UTF-8 JSON body of an <see cref="EventsResult"/> response. The JSON has the shape
/// <c>{"events":[...],"inProgress":bool}</c> and can be deserialized into <see cref="EventsResult"/>.
/// Server-side producers should populate <see cref="InProgress"/> so proxy caches can avoid
/// storing in-progress fights without parsing the full payload.
/// </summary>
public sealed record RawEventsResponse(byte[] JsonBytes, bool? InProgress = null);

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
