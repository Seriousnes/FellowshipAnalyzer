namespace FellowshipAnalyzer.FellowshipLogs;

/// <summary>
/// Streams raw gzip-compressed GraphQL responses from Fellowship Logs directly to the caller,
/// without deserialization. Used by server endpoints to pass bytes through to the browser.
/// </summary>
public interface IFellowshipLogsProxy
{
    Task<HttpResponseMessage> ProxyReportAsync(string reportCode, CancellationToken cancellationToken);

    Task<HttpResponseMessage> ProxyEventsAsync(
        string reportCode,
        int playerId,
        int fightId,
        CancellationToken cancellationToken);
}
