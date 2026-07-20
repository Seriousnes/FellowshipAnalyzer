using System.Diagnostics;
using System.Net.Http.Json;

using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

namespace FellowshipAnalyzer.Services;

public sealed class FellowshipLogsApiClient(HttpClient http, FellowshipAnalyzerJsonContext jsonContext, ILogger<FellowshipLogsApiClient> logger)
{
    public async Task<AnalysisPreload> GetAnalysisPreloadAsync(string reportCode, CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync($"api/analysis/{Uri.EscapeDataString(reportCode)}", jsonContext.AnalysisPreload, ct)
            ?? throw new InvalidOperationException("Analysis preload response was null.");
    }

    public async Task<CharacterReports> GetCharacterReportsAsync(int characterId, CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync($"api/character/{characterId}", jsonContext.CharacterReports, ct)
            ?? throw new InvalidOperationException("Character reports response was null.");
    }

    public async Task<RawEventsResponse> GetRawEventsAsync(
        string reportCode, int playerId, int fightId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var url = $"api/events?reportCode={Uri.EscapeDataString(reportCode)}&playerId={playerId}&fightId={fightId}";
        logger.LogInformation("GetRawEventsAsync sending GET {Url}", url);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        logger.LogInformation(
            "GetRawEventsAsync headers received status={Status} contentLength={ContentLength} t={ElapsedMs}ms",
            (int)response.StatusCode,
            response.Content.Headers.ContentLength,
            sw.ElapsedMilliseconds);
        response.EnsureSuccessStatusCode();

        DateTimeOffset? expiresAt = null;
        if (response.Headers.TryGetValues("X-FellowshipAnalyzer-ExpiresAt", out var values))
        {
            var raw = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(raw)
                && DateTimeOffset.TryParseExact(raw, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                expiresAt = parsed;
            }
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        logger.LogInformation(
            "GetRawEventsAsync body read bytes={Bytes} t={ElapsedMs}ms",
            bytes.Length, sw.ElapsedMilliseconds);
        return new RawEventsResponse(bytes, expiresAt);
    }

    /// <summary>
    /// Fetches all death events for a fight regardless of hostility or killer. Fight-scoped
    /// (no player filter), so the same payload is shared by every combatant in the fight.
    /// </summary>
    public async Task<RawEventsResponse> GetRawDeathsAsync(
        string reportCode, int fightId, CancellationToken ct = default)
    {
        var url = $"api/deaths?reportCode={Uri.EscapeDataString(reportCode)}&fightId={fightId}";
        logger.LogInformation("GetRawDeathsAsync sending GET {Url}", url);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        DateTimeOffset? expiresAt = null;
        if (response.Headers.TryGetValues("X-FellowshipAnalyzer-ExpiresAt", out var values))
        {
            var raw = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(raw)
                && DateTimeOffset.TryParseExact(raw, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                expiresAt = parsed;
            }
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        logger.LogInformation("GetRawDeathsAsync body read bytes={Bytes}", bytes.Length);
        return new RawEventsResponse(bytes, expiresAt);
    }
}

/// <summary>
/// Response from the events API endpoint, including the raw JSON bytes and optional server-side expiry.
/// </summary>
public sealed record RawEventsResponse(byte[] Bytes, DateTimeOffset? ExpiresAt);
