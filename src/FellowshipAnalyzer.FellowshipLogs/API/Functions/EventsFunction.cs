using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed class EventsFunction(IApiRequestExecutor api, FellowshipLogsClientOptions options)
    : BaseFunction(api, options), IEventsFunction
{
    private const string Query = """
        query ReportEvents($code: String!, $fightIDs: [Int!], $sourceID: Int!, $startTime: Float, $limit: Int!) {
          reportData {
            report(code: $code) {
              events(fightIDs: $fightIDs, sourceID: $sourceID, startTime: $startTime, limit: $limit, dataType: All) {
                data
                nextPageTimestamp
              }
            }
          }
        }
        """;

    public async Task<IReadOnlyList<Event>> GetAsync(
        FellowshipLogsEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var events = new List<Event>();
        double? startTime = null;

        while (true)
        {
            var (page, nextPageTimestamp) = await FetchPageAsync(request, startTime, cancellationToken);
            events.AddRange(page);

            if (!nextPageTimestamp.HasValue ||
                (startTime.HasValue && nextPageTimestamp.Value <= startTime.Value))
            {
                break;
            }

            startTime = nextPageTimestamp;
        }

        return events;
    }

    private async Task<(List<Event> Events, double? NextPageTimestamp)> FetchPageAsync(
        FellowshipLogsEventsRequest request,
        double? startTime,
        CancellationToken cancellationToken)
    {
        var payload = await ApiRequestAsync<FellowshipLogsResponse<FellowshipLogsReportResponse>>(
            new
            {
                query = Query,
                variables = new
                {
                    code = request.ReportCode,
                    fightIDs = new[] { request.FightId },
                    sourceID = request.PlayerId,
                    startTime,
                    limit = Math.Clamp(request.PageSize, 100, 10_000)
                }
            },
            cancellationToken);

        var eventsData = payload?.Data?.ReportData?.Report?.Events
            ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

        return (eventsData.Data, eventsData.NextPageTimestamp);
    }

    private static void ValidateRequest(FellowshipLogsEventsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportCode))
            throw new ArgumentException("Report code is required.", nameof(request));
        if (request.PlayerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.PlayerId), "Player ID must be greater than zero.");
        if (request.FightId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.FightId), "Fight ID must be greater than zero.");
    }
}
