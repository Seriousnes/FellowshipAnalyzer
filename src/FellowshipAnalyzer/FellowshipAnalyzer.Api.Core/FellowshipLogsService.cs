using System.Text.Json;

using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.FellowshipLogs;

using StrawberryShake;

namespace FellowshipAnalyzer.Api.Core;

internal sealed record RawEventsResult(byte[] JsonBytes, bool InProgress);

public sealed class FellowshipLogsService(IFellowshipLogsApiClient client, GraphQLMapper mapper)
{
    internal async Task<RawEventsResult> GetRawEventsAsync(
        string reportCode, int playerId, int fightId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));
        if (playerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerId), "Player ID must be greater than zero.");
        if (fightId <= 0)
            throw new ArgumentOutOfRangeException(nameof(fightId), "Fight ID must be greater than zero.");

        var result = await client.GetEvents.ExecuteAsync(
            reportCode, new int?[] { fightId }, playerId, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

        var inProgress = report.Fights?.FirstOrDefault(f => f is not null)?.InProgress ?? false;
        var data = report.Events?.Data;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("inProgress", inProgress);
            writer.WritePropertyName("events");

            if (data is { ValueKind: JsonValueKind.Array } d)
            {
                d.WriteTo(writer);
            }
            else if (data is null)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
            }
            else
            {
                throw new InvalidOperationException($"Unexpected JSON kind for events data: {data.Value.ValueKind}.");
            }

            writer.WriteEndObject();
        }

        return new RawEventsResult(stream.ToArray(), inProgress);
    }

    public async Task<AnalysisPreload> GetReportMasterDataAsync(
        string reportCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));

        var result = await client.GetReportMasterData.ExecuteAsync(reportCode, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected analysis preload data.");

        return mapper.MapAnalysisPreload(reportCode, report);
    }

    public async Task<CharacterReports> GetCharacterReportsAsync(
        int characterId,
        CancellationToken cancellationToken = default)
    {
        if (characterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(characterId), "Character ID must be greater than zero.");

        var result = await client.GetCharacterReports.ExecuteAsync(characterId, cancellationToken);
        ThrowIfErrors(result);

        var character = result.Data!.CharacterData?.Character
            ?? throw new InvalidOperationException("GraphQL response did not contain expected character data.");

        return mapper.MapCharacterReports(character);
    }

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
}
