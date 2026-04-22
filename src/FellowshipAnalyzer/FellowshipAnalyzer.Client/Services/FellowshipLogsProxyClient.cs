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
        MasterData = new ProxyMasterDataFunction(http, jsonOptions);
    }

    public IReportFunction Report { get; }
    public IEventsFunction Events { get; }
    public IMasterDataFunction MasterData { get; }

    private sealed class ProxyReportFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IReportFunction
    {
        public async Task<FellowshipLogsReportInfo> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var response = await http.GetFromJsonAsync<GraphQLResponse<GraphQLReportResponse>>($"api/report/{Uri.EscapeDataString(reportCode)}", jsonOptions, cancellationToken)
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
            var response = await http.GetFromJsonAsync<GraphQLResponse<GraphQLReportResponse>>(
                $"api/events?reportCode={Uri.EscapeDataString(request.ReportCode)}&playerId={request.PlayerId}&fightId={request.FightId}", jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize events response.");

            var report = response.Data.ReportData.Report;
            var eventsData = report.Events
                ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

            var inProgress = report.Fights?.FirstOrDefault()?.InProgress ?? false;

            return new EventsResult(eventsData.Data, inProgress);
        }
    }

    private sealed class ProxyMasterDataFunction(HttpClient http, JsonSerializerOptions jsonOptions) : IMasterDataFunction
    {
        public async Task<FellowshipLogsMasterData> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var response = await http.GetFromJsonAsync<GraphQLResponse<GraphQLReportResponse>>(
                $"api/masterdata/{Uri.EscapeDataString(reportCode)}", jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize master data response.");

            var masterData = response.Data.ReportData.Report.MasterData
                ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

            var abilities = masterData.Abilities?.Select(a => new FellowshipLogsAbility(
                a.GameID, a.Name, a.Icon, a.Type
            )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsAbility>)[];

            var actors = masterData.Actors?.Select(a => new FellowshipLogsActor(
                a.Id, a.Name, a.Type, a.SubType, a.Server
            )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

            return new FellowshipLogsMasterData(abilities, actors);
        }
    }
}
