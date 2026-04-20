using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.API.Functions;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class ApiClient : IFellowshipLogsClient, IFellowshipLogsProxy
{
    private readonly ReportFunction _report;
    private readonly EventsFunction _events;

    public ApiClient(IApiRequestExecutor api, FellowshipLogsClientOptions options, IHttpClientFactory httpClientFactory)
    {
        _report = new ReportFunction(api, options, httpClientFactory);
        _events = new EventsFunction(api, options, httpClientFactory);
    }

    public IReportFunction Report => _report;
    public IEventsFunction Events => _events;

    public Task<HttpResponseMessage> ProxyReportAsync(string reportCode, CancellationToken cancellationToken) =>
        _report.ProxyAsync(reportCode, cancellationToken);

    public Task<HttpResponseMessage> ProxyEventsAsync(string reportCode, int playerId, int fightId, CancellationToken cancellationToken) =>
        _events.ProxyAsync(reportCode, playerId, fightId, cancellationToken);
}
