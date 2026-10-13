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
/// Sends a checked insight message to the model with its guide's system prompt and the configured key, model
/// and effort, drops the insights <see cref="InsightValidation"/> refuses, and keeps each answer in
/// <see cref="CachePartition.Insights"/>.
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

    /// <summary>Insights on <paramref name="guide"/> of a <paramref name="hero"/> run, from the insight message <paramref name="body"/>.</summary>
    public async Task<GuideInsightsOutcome> GetAsync(string hero, string guide, byte[] body, CancellationToken cancellationToken)
    {
        var (request, failure) = InsightRequestValidation.Check(hero, guide, body);
        if (request is null)
            return GuideInsightsOutcome.Fail(failure!.StatusCode, failure.Error);

        var key = CacheKey(request);
        if (await ReadCachedAsync(key, cancellationToken) is { } cached)
            return new GuideInsightsOutcome(200, cached with { Insights = InsightValidation.Check(cached.Insights, request.Guide).Accepted }, null);

        var result = await GuideInsights.RunAsync(_chat.Value, _options.Model, _options.Effort, request.Guide.System, request.User, cancellationToken);
        if (result.Parsed?["insights"] is not JsonArray insights)
        {
            _logger.LogWarning("Insights {Hero} {Guide} failed: {Error}", hero, guide, result.Error);
            return GuideInsightsOutcome.Fail(502, result.Error ?? "The model returned no insights.");
        }

        var validation = InsightValidation.Check(
            [.. insights.OfType<JsonObject>().Select(insight => new GuideInsight((string?)insight["title"] ?? "", (string?)insight["insight"] ?? ""))],
            request.Guide);
        var response = new GuideInsightsResponse(request.Guide.Hero, request.Guide.Guide, validation.Accepted);
        _logger.LogInformation(
            "Insights {Hero} {Guide}: {Insights} insights, {Refused} refused, ${Cost} in {Latency}s",
            hero, guide, response.Insights.Count, validation.Rejections.Count, result.CostUsd, Math.Round(result.Latency.TotalSeconds, 2));

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

    private string CacheKey(InsightRequestValidation.Accepted request)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n\u0000", _options.Model, _options.Effort, request.Guide.System, request.User)));
        return $"{request.Guide.Hero}/{request.Guide.Guide}/{Convert.ToHexString(hash).ToLowerInvariant()}.json";
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
