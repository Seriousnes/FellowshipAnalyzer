using System.Net.Http.Json;
using System.Text.Json;

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

    public Task<byte[]> GetRawEventsAsync(string reportCode, int playerId, int fightId, CancellationToken ct = default)
        => http.GetByteArrayAsync(
            $"api/events?reportCode={Uri.EscapeDataString(reportCode)}&playerId={playerId}&fightId={fightId}",
            ct);
}
