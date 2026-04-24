using System.Net.Http.Json;
using System.Text.Json;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// WASM-side implementation of <see cref="IFellowshipLogsClient"/> that calls the server
/// proxy endpoints and deserializes typed domain responses.
/// </summary>
public sealed class FellowshipLogsProxyClient : IFellowshipLogsClient
{
    public FellowshipLogsProxyClient(HttpClient http, JsonSerializerOptions jsonOptions)
    {
        var analysisPreload = new ProxyAnalysisPreloadFunction(http, jsonOptions);
        Report = new ProxyReportFunction(analysisPreload);
        Events = new ProxyEventsFunction(http, jsonOptions);
        MasterData = new ProxyMasterDataFunction(analysisPreload);
        AnalysisPreload = analysisPreload;
    }

    public IReportFunction Report { get; }
    public IEventsFunction Events { get; }
    public IMasterDataFunction MasterData { get; }
    public IAnalysisPreloadFunction AnalysisPreload { get; }

    private sealed class ProxyAnalysisPreloadFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IAnalysisPreloadFunction
    {
        public async Task<AnalysisPreload> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            return await http.GetFromJsonAsync<AnalysisPreload>(
                $"api/analysis/{Uri.EscapeDataString(reportCode)}", jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize analysis preload response.");
        }
    }

    private sealed class ProxyReportFunction(ProxyAnalysisPreloadFunction analysisPreload) : IReportFunction
    {
        public async Task<ReportInfo> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var preload = await analysisPreload.GetAsync(reportCode, cancellationToken);
            return preload.ReportInfo;
        }
    }

    private sealed class ProxyEventsFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IEventsFunction
    {
        public async Task<EventsResult> GetAsync(
            FellowshipLogsEventsRequest request,
            CancellationToken cancellationToken = default)
        {
            return await http.GetFromJsonAsync<EventsResult>(
                $"api/events?reportCode={Uri.EscapeDataString(request.ReportCode)}&playerId={request.PlayerId}&fightId={request.FightId}",
                jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize events response.");
        }
    }

    private sealed class ProxyMasterDataFunction(ProxyAnalysisPreloadFunction analysisPreload) : IMasterDataFunction
    {
        public async Task<ReportMasterData> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var preload = await analysisPreload.GetAsync(reportCode, cancellationToken);
            return preload.MasterData;
        }
    }
}
