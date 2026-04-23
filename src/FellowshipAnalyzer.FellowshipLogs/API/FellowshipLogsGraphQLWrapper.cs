using System.Text.Json;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using StrawberryShake;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class FellowshipLogsGraphQLWrapper : IFellowshipLogsClient
{
    public FellowshipLogsGraphQLWrapper(IFellowshipLogsApiClient client, JsonSerializerOptions jsonOptions)
    {
        Report = new GraphQLReportFunction(client);
        Events = new GraphQLEventsFunction(client, jsonOptions);
        var analysisPreload = new GraphQLAnalysisPreloadFunction(client);
        MasterData = new GraphQLMasterDataFunction(analysisPreload);
        AnalysisPreload = analysisPreload;
    }

    public IReportFunction Report { get; }
    public IEventsFunction Events { get; }
    public IMasterDataFunction MasterData { get; }
    public IAnalysisPreloadFunction AnalysisPreload { get; }

    private static void ThrowIfErrors<T>(IOperationResult<T> result) where T : class
    {
        if (result.Errors is { Count: > 0 })
        {
            var messages = string.Join("; ", result.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"GraphQL errors: {messages}");
        }
        if (result.Data is null)
            throw new InvalidOperationException("GraphQL response contained no data.");
    }

    private static FellowshipLogsFight MapFight(
        int id, string name, int encounterID, bool? kill,
        double startTime, double endTime, int? difficulty,
        IReadOnlyList<int?>? friendlyPlayers, double? fightPercentage, bool inProgress)
    {
        var fp = friendlyPlayers?
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList()
            .AsReadOnly();
        return new FellowshipLogsFight(id, name, encounterID, kill, startTime, endTime,
            difficulty, fp, fightPercentage, inProgress);
    }

    private static IReadOnlyList<FellowshipLogsFight> MapFights(
        IReadOnlyList<IGetReport_ReportData_Report_Fights?>? fights)
    {
        if (fights is null) return [];
        return fights
            .Where(f => f is not null)
            .Select(f => MapFight(f!.Id, f.Name, f.EncounterID, f.Kill,
                f.StartTime, f.EndTime, f.Difficulty, f.FriendlyPlayers,
                f.FightPercentage, f.InProgress))
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<FellowshipLogsFight> MapFights(
        IReadOnlyList<IGetAnalysisPreload_ReportData_Report_Fights?>? fights)
    {
        if (fights is null) return [];
        return fights
            .Where(f => f is not null)
            .Select(f => MapFight(f!.Id, f.Name, f.EncounterID, f.Kill,
                f.StartTime, f.EndTime, f.Difficulty, f.FriendlyPlayers,
                f.FightPercentage, f.InProgress))
            .ToList()
            .AsReadOnly();
    }

    private static FellowshipLogsMasterData MapMasterData(
        IReadOnlyList<IGetAnalysisPreload_ReportData_Report_MasterData_Abilities?>? abilities,
        IReadOnlyList<IGetAnalysisPreload_ReportData_Report_MasterData_Actors?>? actors)
    {
        var mappedAbilities = abilities?
            .Where(a => a is not null)
            .Select(a => new FellowshipLogsAbility(a!.GameID, a.Name, a.Icon, a.Type))
            .ToList()
            .AsReadOnly() ?? (IReadOnlyList<FellowshipLogsAbility>)[];

        var mappedActors = actors?
            .Where(a => a is not null)
            .Select(a => new FellowshipLogsActor(a!.Id, a.Name, a.Type, a.SubType, a.Server, a.Icon))
            .ToList()
            .AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

        return new FellowshipLogsMasterData(mappedAbilities, mappedActors);
    }

    private sealed class GraphQLReportFunction(IFellowshipLogsApiClient client) : IReportFunction
    {
        public async Task<FellowshipLogsReportInfo> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reportCode))
                throw new ArgumentException("Report code is required.", nameof(reportCode));

            var result = await client.GetReport.ExecuteAsync(reportCode, cancellationToken);
            ThrowIfErrors(result);

            var report = result.Data!.ReportData?.Report
                ?? throw new InvalidOperationException("GraphQL response did not contain expected report data.");

            var fights = MapFights(report.Fights);
            var actors = report.MasterData?.Actors?
                .Where(a => a is not null)
                .Select(a => new FellowshipLogsActor(a!.Id, a.Name, a.Type, a.SubType, a.Server, a.Icon))
                .ToList()
                .AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

            return new FellowshipLogsReportInfo(reportCode, report.Title, report.StartTime, report.EndTime, fights, actors);
        }
    }

    private sealed class GraphQLEventsFunction(IFellowshipLogsApiClient client, JsonSerializerOptions jsonOptions) : IEventsFunction
    {
        public async Task<EventsResult> GetAsync(
            FellowshipLogsEventsRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.ReportCode))
                throw new ArgumentException("Report code is required.", nameof(request));
            if (request.PlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.PlayerId), "Player ID must be greater than zero.");
            if (request.FightId <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.FightId), "Fight ID must be greater than zero.");

            var result = await client.GetEvents.ExecuteAsync(
                request.ReportCode,
                new int?[] { request.FightId },
                request.PlayerId,
                cancellationToken);
            ThrowIfErrors(result);

            var report = result.Data!.ReportData?.Report
                ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

            var inProgress = report.Fights?.FirstOrDefault(f => f is not null)?.InProgress ?? false;

            var data = report.Events?.Data;
            List<Event> events;
            if (data is { } d && d.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                events = JsonSerializer.Deserialize<List<Event>>(d, jsonOptions) ?? [];
            }
            else if (data is null)
            {
                events = [];
            }
            else
            {
                throw new InvalidOperationException($"Unexpected JSON kind for events data: {data.Value.ValueKind}.");
            }

            return new EventsResult(events, inProgress);
        }
    }

    private sealed class GraphQLMasterDataFunction(GraphQLAnalysisPreloadFunction analysisPreload) : IMasterDataFunction
    {
        public async Task<FellowshipLogsMasterData> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            var preload = await analysisPreload.GetAsync(reportCode, cancellationToken);
            return preload.MasterData;
        }
    }

    private sealed class GraphQLAnalysisPreloadFunction(IFellowshipLogsApiClient client) : IAnalysisPreloadFunction
    {
        public async Task<FellowshipLogsAnalysisPreload> GetAsync(
            string reportCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reportCode))
                throw new ArgumentException("Report code is required.", nameof(reportCode));

            var result = await client.GetAnalysisPreload.ExecuteAsync(reportCode, cancellationToken);
            ThrowIfErrors(result);

            var report = result.Data!.ReportData?.Report
                ?? throw new InvalidOperationException("GraphQL response did not contain expected analysis preload data.");

            var masterData = report.MasterData
                ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

            var mapped = MapMasterData(masterData.Abilities, masterData.Actors);
            var fights = MapFights(report.Fights);
            var reportInfo = new FellowshipLogsReportInfo(
                reportCode, report.Title, report.StartTime, report.EndTime, fights, mapped.Actors);

            return new FellowshipLogsAnalysisPreload(reportInfo, mapped);
        }
    }
}
