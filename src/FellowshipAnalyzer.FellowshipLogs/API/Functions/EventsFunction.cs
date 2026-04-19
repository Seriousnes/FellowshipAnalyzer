using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal sealed class EventsFunction(IApiRequestExecutor api, FellowshipLogsClientOptions options)
    : BaseFunction(api, options), IEventsFunction
{
    private const string Query = """
        query ReportEvents($code: String!, $fightIDs: [Int!], $sourceID: Int!) {          
          reportData {
            report(code: $code) {
              fights(fightIDs: $fightIDs) {
                inProgress                
              }
              events(fightIDs: $fightIDs, sourceID: $sourceID, useAbilityIDs: true) {
                data
                nextPageTimestamp
              }
            }
          }
        }
        """;

    public async Task<EventsResult> GetAsync(
        FellowshipLogsEventsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var events = new List<Event>();
        double? startTime = null;
        var inProgress = false;

        while (true)
        {
            var (page, nextPageTimestamp, pageInProgress) = await FetchPageAsync(request, startTime, cancellationToken);
            events.AddRange(page);
            inProgress = pageInProgress;

            if (!nextPageTimestamp.HasValue ||
                (startTime.HasValue && nextPageTimestamp.Value <= startTime.Value))
            {
                break;
            }

            startTime = nextPageTimestamp;
        }

        return new EventsResult(events, inProgress);
    }

    private async Task<(List<Event> Events, double? NextPageTimestamp, bool InProgress)> FetchPageAsync(
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
                    sourceID = request.PlayerId
                }
            },
            cancellationToken);

        var eventsData = payload?.Data?.ReportData?.Report?.Events
            ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

        // The root-level fights array contains live status for the requested fight IDs.
        var inProgress = payload?.Data?.Fights?.FirstOrDefault()?.InProgress ?? false;

        return (eventsData.Data, eventsData.NextPageTimestamp, inProgress);
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
