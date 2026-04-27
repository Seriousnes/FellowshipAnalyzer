using System.Text.Json;

using StrawberryShake;

namespace FellowshipAnalyzer.Api.Core;

public sealed class FellowshipLogsService(IFellowshipLogsApiClient client)
{
    public async Task<RawEventsResult> GetRawEventsAsync(
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

    public async Task<AnalysisPreloadResponse> GetReportMasterDataAsync(
        string reportCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            throw new ArgumentException("Report code is required.", nameof(reportCode));

        var result = await client.GetReportMasterData.ExecuteAsync(reportCode, cancellationToken);
        ThrowIfErrors(result);

        var report = result.Data!.ReportData?.Report
            ?? throw new InvalidOperationException("GraphQL response did not contain expected analysis preload data.");

        var masterData = report.MasterData
            ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

        var abilities = masterData.Abilities?
            .Where(a => a is not null)
            .Select(a =>
            {
                if (!int.TryParse(a!.Type, out var typeInt)) typeInt = 0;
                return new AbilityResponse(a.GameID, a.Name, a.Icon, typeInt);
            })
            .ToList() ?? [];

        var actors = masterData.Actors?
            .Where(a => a is not null)
            .Select(a => new ActorResponse(a!.Id, a.Name, a.Type, a.SubType, a.Server, a.Icon))
            .ToList() ?? [];

        var fights = report.Fights?
            .Where(f => f is not null)
            .Select(f =>
            {
                var fp = f!.FriendlyPlayers?
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();
                return new FightResponse(f.Id, f.Name, f.EncounterID, f.Kill,
                    f.StartTime, f.EndTime, f.Difficulty, fp, f.FightPercentage, f.InProgress);
            })
            .ToList() ?? [];

        var reportInfo = new ReportInfoResponse(
            reportCode, report.Title, report.StartTime, report.EndTime, fights, actors);

        return new AnalysisPreloadResponse(reportInfo, new MasterDataResponse(abilities, actors));
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
