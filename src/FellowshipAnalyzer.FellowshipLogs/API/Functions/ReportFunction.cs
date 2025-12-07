using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed partial class ReportFunction(IApiRequestExecutor api, FellowshipLogsClientOptions options)
    : BaseFunction(api, options), IReportFunction
{
    public async Task<FellowshipLogsReportInfo> GetAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
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
            f.FriendlyPlayers is { } fp ? fp.AsReadOnly() : null
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsFight>)[];

        var actors = report.MasterData?.Actors?.Select(a => new FellowshipLogsActor(
            a.Id, a.Name, a.Type, a.SubType, a.Server
        )).ToList().AsReadOnly() ?? (IReadOnlyList<FellowshipLogsActor>)[];

        return new FellowshipLogsReportInfo(
            reportCode, report.Title, report.StartTime, report.EndTime, fights, actors);
    }
}
