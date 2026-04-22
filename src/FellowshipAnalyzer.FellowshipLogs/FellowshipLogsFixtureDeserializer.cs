using System.Text.Json;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.FellowshipLogs.API;
using FellowshipAnalyzer.FellowshipLogs.Extensions;

namespace FellowshipAnalyzer.FellowshipLogs;

public static class FellowshipLogsFixtureDeserializer
{
    public static IReadOnlyList<Event> DeserializeEvents(string json, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var options = jsonSerializerOptions ?? ServiceCollectionExtensions.CreateJsonSerializerOptions();
        var response = JsonSerializer.Deserialize<FellowshipLogsResponse<FellowshipLogsReportResponse>>(json, options)
            ?? throw new InvalidOperationException("Unable to deserialize Fellowship Logs payload.");

        return response.Data?.ReportData?.Report?.Events?.Data
            ?? throw new InvalidOperationException("Payload did not contain event data.");
    }
}