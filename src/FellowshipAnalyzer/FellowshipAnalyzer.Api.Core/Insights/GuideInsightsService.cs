using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Anthropic;

using FellowshipAnalyzer.Api.Core.Caching;
using FellowshipAnalyzer.Core.Insights;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>The insights on one guide: a response, or an HTTP status and message.</summary>
/// <param name="StatusCode">200 with <paramref name="Response"/>, otherwise the error status.</param>
/// <param name="Response">The insights.</param>
/// <param name="Error">Why there is no response.</param>
public sealed record GuideInsightsOutcome(int StatusCode, GuideInsightsResponse? Response, string? Error)
{
    internal static GuideInsightsOutcome Fail(int statusCode, string error) => new(statusCode, null, error);
}

/// <summary>
/// Sends a checked <see cref="GuideInsightsRequest"/> to the model with the configured key, model and effort,
/// and keeps each answer in <see cref="CachePartition.Insights"/>.
/// </summary>
public sealed class GuideInsightsService : IDisposable
{
    private readonly GuideInsightsOptions _options;
    private readonly IPersistentCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<GuideInsightsService> _logger;
    private readonly Lazy<IChatClient> _chat;

    public GuideInsightsService(
        GuideInsightsOptions options,
        IPersistentCache cache,
        JsonSerializerOptions jsonOptions,
        ILogger<GuideInsightsService> logger)
    {
        _options = options;
        _cache = cache;
        _jsonOptions = jsonOptions;
        _logger = logger;
        _chat = new Lazy<IChatClient>(CreateChatClient);
    }

    /// <summary>Whether insights are switched on and a model key is configured.</summary>
    public bool Available => _options.Available;

    /// <summary>Largest request body the endpoint reads, in bytes.</summary>
    public long MaxRequestBytes => _options.MaxRequestBytes;

    /// <summary>Insights on <paramref name="guide"/> of a <paramref name="hero"/> run.</summary>
    public async Task<GuideInsightsOutcome> GetAsync(string hero, string guide, GuideInsightsRequest? request, CancellationToken cancellationToken)
    {
        if (InsightRequestValidation.Check(hero, guide, request, _options.MaxRequestBytes) is { } failure)
            return GuideInsightsOutcome.Fail(failure.StatusCode, failure.Error);

        var key = CacheKey(hero.ToLowerInvariant(), guide, request!);
        if (await ReadCachedAsync(key, cancellationToken) is { } cached)
            return new GuideInsightsOutcome(200, cached, null);

        var result = await GuideInsights.RunAsync(_chat.Value, _options.Model, _options.Effort, request!.System, request.User, cancellationToken);
        if (result.Parsed?["insights"] is not JsonArray insights)
        {
            _logger.LogWarning("Insights {Hero} {Guide} failed: {Error}", hero, guide, result.Error);
            return GuideInsightsOutcome.Fail(502, result.Error ?? "The model returned no insights.");
        }

        var response = new GuideInsightsResponse(
            hero.ToLowerInvariant(),
            guide,
            [.. insights.OfType<JsonObject>().Select(insight => new GuideInsight((string?)insight["title"] ?? "", (string?)insight["insight"] ?? ""))]);
        _logger.LogInformation(
            "Insights {Hero} {Guide}: {Insights} insights, ${Cost} in {Latency}s",
            hero, guide, response.Insights.Count, result.CostUsd, Math.Round(result.Latency.TotalSeconds, 2));

        await _cache.SetAsync(
            CachePartition.Insights,
            key,
            JsonSerializer.SerializeToUtf8Bytes(response, _jsonOptions),
            new PersistentCacheWriteOptions(DateTimeOffset.UtcNow.Add(_options.CacheDuration), "application/json", null),
            cancellationToken);
        return new GuideInsightsOutcome(200, response, null);
    }

    public void Dispose()
    {
        if (_chat.IsValueCreated)
            _chat.Value.Dispose();
    }

    private async Task<GuideInsightsResponse?> ReadCachedAsync(string key, CancellationToken cancellationToken)
    {
        var entry = await _cache.GetAsync(CachePartition.Insights, key, cancellationToken);
        if (entry is null)
            return null;
        await using var content = entry.Content;
        return await JsonSerializer.DeserializeAsync<GuideInsightsResponse>(content, _jsonOptions, cancellationToken);
    }

    private string CacheKey(string hero, string guide, GuideInsightsRequest request)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n\u0000", _options.Model, _options.Effort, request.System, request.User)));
        return $"{hero}/{guide}/{Convert.ToHexString(hash).ToLowerInvariant()}.json";
    }

    private IChatClient CreateChatClient()
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(_options.WorkspaceId))
            headers["anthropic-workspace-id"] = _options.WorkspaceId;
        var client = new AnthropicClient(new Anthropic.Core.ClientOptions { ApiKey = _options.ApiKey!, ExtraHeaders = headers });
        return client.AsIChatClient(_options.Model);
    }
}
