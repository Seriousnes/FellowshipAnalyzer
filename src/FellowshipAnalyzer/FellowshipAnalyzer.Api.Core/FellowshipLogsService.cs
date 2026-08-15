using System.Text.Json;

using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.IO;

using StrawberryShake;

namespace FellowshipAnalyzer.Api.Core;

internal sealed record RawEventsResult(byte[] JsonBytes, bool InProgress, bool HasEvents);

internal sealed record UsageFacts(string? Hero, string? Dungeon);

public sealed class FellowshipLogsService(IFellowshipLogsApiClient client, GraphQLMapper mapper, RecyclableMemoryStreamManager streamManager)
{
    internal async Task<RawEventsResult> GetRawEventsAsync(
        string reportCode, int playerId, int dungeonId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));
        if (playerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerId), "Player ID must be greater than zero.");
        if (dungeonId <= 0)
            throw new ArgumentOutOfRangeException(nameof(dungeonId), "Dungeon ID must be greater than zero.");

        var result = await client.GetEvents.ExecuteAsync(
            reportCode, new int?[] { dungeonId }, playerId, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected event data.");

        var inProgress = report.Fights?.FirstOrDefault(f => f is not null)?.InProgress ?? false;
        var data = report.Events?.Data;
        var hasEvents = false;

        using var stream = streamManager.GetStream("fellowship-events");
        using (var writer = new Utf8JsonWriter((Stream)stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("inProgress", inProgress);
            writer.WritePropertyName("events");

            if (data is { ValueKind: JsonValueKind.Array } d)
            {
                hasEvents = d.GetArrayLength() > 0;
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

        return new RawEventsResult(stream.ToArray(), inProgress, hasEvents);
    }

    /// <summary>
    /// Fetches all death events for a dungeon regardless of who landed the kill or which side died.
    /// The Fellowship Logs <c>events(dataType: Deaths)</c> stream is split by <c>hostilityType</c>:
    /// the enemy view returns enemy deaths, the friendly view returns player deaths. Neither value
    /// returns both, so both are fetched and concatenated into a single <c>events</c> array. This is
    /// dungeon-scoped (no player filter), so it is shared by every combatant in the dungeon.
    /// </summary>
    internal async Task<RawEventsResult> GetRawDeathsAsync(
        string reportCode, int dungeonId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));
        if (dungeonId <= 0)
            throw new ArgumentOutOfRangeException(nameof(dungeonId), "Dungeon ID must be greater than zero.");

        var result = await client.GetDeaths.ExecuteAsync(
            reportCode, new int?[] { dungeonId }, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected death data.");

        var inProgress = report.Fights?.FirstOrDefault(f => f is not null)?.InProgress ?? false;
        var deathCount = 0;

        using var stream = streamManager.GetStream("fellowship-deaths");
        using (var writer = new Utf8JsonWriter((Stream)stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("inProgress", inProgress);
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            deathCount += WriteDeaths(writer, report.EnemyDeaths?.Data);
            deathCount += WriteDeaths(writer, report.FriendlyDeaths?.Data);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return new RawEventsResult(stream.ToArray(), inProgress, deathCount > 0);

        static int WriteDeaths(Utf8JsonWriter writer, JsonElement? data)
        {
            if (data is not { ValueKind: JsonValueKind.Array } array)
            {
                return 0;
            }

            var count = 0;
            foreach (var element in array.EnumerateArray())
            {
                element.WriteTo(writer);
                count++;
            }
            return count;
        }
    }

    internal async Task<UsageFacts> GetUsageFactsAsync(
        string reportCode, int playerId, int dungeonId,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetUsageFacts.ExecuteAsync(
            reportCode, new int?[] { dungeonId }, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report;

        var dungeon = report?.Fights?.FirstOrDefault(fight => fight is not null)?.Name;
        var hero = report?.MasterData?.Actors?
            .FirstOrDefault(actor => actor?.Id == playerId)?.SubType;

        return new UsageFacts(hero, dungeon);
    }

    public async Task<AnalysisPreload> GetReportMasterDataAsync(
        string reportCode, int? dungeonId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));

        int?[] dungeonIds = dungeonId is { } id ? [id] : [];
        var result = await client.GetReportMasterData.ExecuteAsync(
            reportCode, dungeonIds, dungeonId is not null, cancellationToken);
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
