using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed partial class ReportFunction(IApiRequestExecutor api, FellowshipLogsClientOptions options, IHttpClientFactory httpClientFactory)
    : BaseFunction(api, options, httpClientFactory), IReportFunction, IMasterDataFunction
{
    async Task<FellowshipLogsReportInfo> IReportFunction.GetAsync(
        string reportCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));

        var payload = await ApiRequestAsync<FellowshipLogsResponse<FellowshipLogsReportResponse>>(
            new
            {
                query = ReportQueryString,
                variables = new { code = reportCode }
            },
            cancellationToken);

        var report = payload?.Data?.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected report data.");

        var fights = report.Fights?.Select(f => new FellowshipLogsFight(
            f.Id, f.Name, f.EncounterID, f.Kill, f.StartTime, f.EndTime, f.Difficulty,
            f.FriendlyPlayers is { } fp ? fp.AsReadOnly() : null,
            f.FightPercentage,
            f.InProgress
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsFight>)[];

        var actors = report.MasterData?.Actors?.Select(a => new FellowshipLogsActor(
            a.Id, a.Name, a.Type, a.SubType, a.Server, a.Icon
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

        return new FellowshipLogsReportInfo(
            reportCode, report.Title, report.StartTime, report.EndTime, fights, actors);
    }

    async Task<FellowshipLogsMasterData> IMasterDataFunction.GetAsync(
        string reportCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));

        var payload = await ApiRequestAsync<FellowshipLogsResponse<FellowshipLogsReportResponse>>(
            new
            {
                query = MasterDataQueryString,
                variables = new { code = reportCode }
            },
            cancellationToken);

        var masterData = payload?.Data?.ReportData?.Report?.MasterData
            ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

        var abilities = masterData.Abilities?.Select(a => new FellowshipLogsAbility(
            a.GameID, a.Name, a.Icon, a.Type
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsAbility>)[];

        var actors = masterData.Actors?.Select(a => new FellowshipLogsActor(
            a.Id, a.Name, a.Type, a.SubType, a.Server, a.Icon
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

        return new FellowshipLogsMasterData(abilities, actors);
    }

    public Task<HttpResponseMessage> ProxyAsync(string reportCode, CancellationToken cancellationToken) =>
        ApiProxyRequestAsync(
            new { query = ReportQueryString, variables = new { code = reportCode } },
            cancellationToken);

    public Task<HttpResponseMessage> ProxyMasterDataAsync(string reportCode, CancellationToken cancellationToken) =>
        ApiProxyRequestAsync(
            new { query = MasterDataQueryString, variables = new { code = reportCode } },
            cancellationToken);
}
