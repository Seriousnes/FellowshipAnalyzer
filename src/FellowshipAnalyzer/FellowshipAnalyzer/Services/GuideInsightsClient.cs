using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using FellowshipAnalyzer.Core.Insights;
using FellowshipAnalyzer.Core.Serialization;

namespace FellowshipAnalyzer.Services;

/// <summary>Posts guide insights requests to <c>api/insights/{hero}/{guide}</c>.</summary>
public sealed class GuideInsightsClient : IGuideInsightsClient
{
    private readonly HttpClient _http;
    private readonly FellowshipAnalyzerJsonContext _jsonContext;

    /// <summary>Starts reading <c>api/insights</c>, so <see cref="Available"/> is read once, when the app loads.</summary>
    public GuideInsightsClient(HttpClient http, FellowshipAnalyzerJsonContext jsonContext)
    {
        _http = http;
        _jsonContext = jsonContext;
        Available = ReadAvailableAsync();
    }

    public Task<bool> Available { get; }

    public async Task<GuideInsightsResponse> GetAsync(string hero, string guide, GuideInsightsRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"api/insights/{Uri.EscapeDataString(hero.ToLowerInvariant())}/{Uri.EscapeDataString(guide)}",
            request,
            _jsonContext.GuideInsightsRequest,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Insights failed ({(int)response.StatusCode}): {await ErrorMessageAsync(response, cancellationToken)}");
        }
        return await response.Content.ReadFromJsonAsync(_jsonContext.GuideInsightsResponse, cancellationToken)
            ?? throw new InvalidOperationException("Insights response was null.");
    }

    private async Task<bool> ReadAvailableAsync()
    {
        try
        {
            using var response = await _http.GetAsync("api/insights");
            return response.IsSuccessStatusCode
                && await response.Content.ReadFromJsonAsync(_jsonContext.InsightsAvailability) is { Available: true };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var fallback = response.ReasonPhrase ?? "The insights service did not answer.";
        try
        {
            return JsonNode.Parse(body)?["error"]?.GetValue<string>() ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
