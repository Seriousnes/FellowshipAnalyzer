using System.Text.Json;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.FellowshipLogs.Extensions;

namespace FellowshipAnalyzer.FellowshipLogs;

public static class FellowshipLogsFixtureDeserializer
{
    public static IReadOnlyList<Event> DeserializeEvents(string json, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var options = jsonSerializerOptions ?? ServiceCollectionExtensions.CreateJsonSerializerOptions();
        using var doc = JsonDocument.Parse(json);
        var eventsElement = doc.RootElement
            .GetProperty("data")
            .GetProperty("reportData")
            .GetProperty("report")
            .GetProperty("events")
            .GetProperty("data");

        return JsonSerializer.Deserialize<IReadOnlyList<Event>>(eventsElement, options)
            ?? throw new InvalidOperationException("Payload did not contain event data.");
    }
}