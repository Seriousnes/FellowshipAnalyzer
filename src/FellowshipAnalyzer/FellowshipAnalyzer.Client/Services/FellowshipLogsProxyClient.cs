using System.Net.Http.Json;
using System.Text.Json;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// WASM-side implementation of <see cref="IFellowshipLogsClient"/> that calls the server
/// proxy endpoints and deserializes the raw GraphQL responses into public domain types.
/// </summary>
public sealed class FellowshipLogsProxyClient : IFellowshipLogsClient
{
    public FellowshipLogsProxyClient(HttpClient http, JsonSerializerOptions jsonOptions)
    {
        Report = new ProxyReportFunction(http, jsonOptions);
        Events = new ProxyEventsFunction(http, jsonOptions);
    }

    public IReportFunction Report { get; }
    public IEventsFunction Events { get; }

    private sealed class ProxyReportFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IReportFunction
    {
        public async Task<FellowshipLogsReportInfo> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var url = $"/api/report/{Uri.EscapeDataString(reportCode)}";
            var response = await http.GetFromJsonAsync<GraphQLResponse<GraphQLReportResponse>>(
                url, jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize report response.");

            var report = response.Data.ReportData.Report;

            var fights = report.Fights?.Select(f => new FellowshipLogsFight(
                f.Id, f.Name, f.EncounterID, f.Kill, f.StartTime, f.EndTime, f.Difficulty,
                f.FriendlyPlayers?.AsReadOnly(),
                f.InProgress
            )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsFight>)[];

            var actors = report.MasterData?.Actors?.Select(a => new FellowshipLogsActor(
                a.Id, a.Name, a.Type, a.SubType, a.Server
            )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

            return new FellowshipLogsReportInfo(
                reportCode, report.Title, report.StartTime, report.EndTime, fights, actors);
        }
    }

    private sealed class ProxyEventsFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IEventsFunction
    {
        public async Task<EventsResult> GetAsync(
            FellowshipLogsEventsRequest request,
            CancellationToken cancellationToken = default)
        {
            var url = $"/api/events?reportCode={Uri.EscapeDataString(request.ReportCode)}" +
                      $"&playerId={request.PlayerId}&fightId={request.FightId}";

            var response = await http.GetFromJsonAsync<GraphQLResponse<GraphQLReportResponse>>(
                url, jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize events response.");

            var report = response.Data.ReportData.Report;
            var eventsData = report.Events
                ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

            var inProgress = report.Fights?.FirstOrDefault()?.InProgress ?? false;

            return new EventsResult(eventsData.Data, inProgress);
        }
    }
}
