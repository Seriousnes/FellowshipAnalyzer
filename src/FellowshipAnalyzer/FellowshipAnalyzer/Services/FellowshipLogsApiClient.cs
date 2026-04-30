using System.Net.Http.Json;

using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

namespace FellowshipAnalyzer.Services;

public sealed class FellowshipLogsApiClient(HttpClient http, FellowshipAnalyzerJsonContext jsonContext)
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

    public Task<byte[]> GetRawEventsAsync(string reportCode, int playerId, int fightId, CancellationToken ct = default)
        => http.GetByteArrayAsync(
            $"api/events?reportCode={Uri.EscapeDataString(reportCode)}&playerId={playerId}&fightId={fightId}",
            ct);
}
