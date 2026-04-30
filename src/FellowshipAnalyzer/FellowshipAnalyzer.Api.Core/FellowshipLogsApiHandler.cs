using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// FellowshipLogs API endpoints. Returns <see cref="IResult"/>; the source-generated
/// per-host adapters (Minimal API extension, Functions wrapper) call into these methods.
/// CORS and security headers are applied by the <c>UseFellowshipLogsApi</c> middleware.
/// </summary>
public sealed class FellowshipLogsApiHandler(
    FellowshipLogsService fellowshipLogsService,
    IMemoryCache cache,
    FellowshipLogsCacheOptions cacheOptions,
    FellowshipLogsRateLimiter rateLimiter,
    JsonSerializerOptions jsonOptions)
{
    [ApiEndpoint("GET", "events")]
    public async Task<IResult> GetEventsAsync(
        HttpContext context,
        string? reportCode,
        int? playerId,
        int? fightId,
        CancellationToken cancellationToken)
    {
        if (await TryApplyRateLimitAsync(context, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest("Missing required query parameter 'reportCode'.");
        }
        if (playerId is null)
        {
            return BadRequest("Missing required query parameter 'playerId'.");
        }
        if (fightId is null)
        {
            return BadRequest("Missing required query parameter 'fightId'.");
        }

        var trimmedReportCode = reportCode.Trim();
        var cacheKey = CacheKeys.Events(trimmedReportCode, playerId.Value, fightId.Value);

        if (cache.TryGetValue(cacheKey, out RawEventsResult? cachedResult) && cachedResult is not null)
        {
            ApplyCompletedEventsCacheHeaders(context.Response, hit: true);
            return Results.Bytes(cachedResult.JsonBytes, "application/json");
        }

        var result = await fellowshipLogsService.GetRawEventsAsync(
            trimmedReportCode, playerId.Value, fightId.Value, cancellationToken);

        if (!result.InProgress)
        {
            cache.Set(cacheKey, result, CreateCompletedEventsCacheEntryOptions(cacheOptions));
            ApplyCompletedEventsCacheHeaders(context.Response, hit: false);
        }
        else
        {
            ApplyNoStoreCacheHeaders(context.Response, hit: false);
        }

        return Results.Bytes(result.JsonBytes, "application/json");
    }

    [ApiEndpoint("GET", "analysis/{reportCode}")]
    public async Task<IResult> GetAnalysisAsync(
        HttpContext context,
        string reportCode,
        CancellationToken cancellationToken)
    {
        if (await TryApplyRateLimitAsync(context, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest("Route parameter 'reportCode' is required.");
        }

        var cacheKey = CacheKeys.Analysis(reportCode);

        if (cache.TryGetValue(cacheKey, out AnalysisPreload? cachedPreload) && cachedPreload is not null)
        {
            ApplyAnalysisPreloadCacheHeaders(context.Response, cachedPreload, hit: true);
            return Json(cachedPreload);
        }

        var preload = await fellowshipLogsService.GetReportMasterDataAsync(reportCode, cancellationToken);
        cache.Set(cacheKey, preload, CreateAnalysisPreloadCacheEntryOptions(preload, cacheOptions));
        ApplyAnalysisPreloadCacheHeaders(context.Response, preload, hit: false);

        return Json(preload);
    }

    [ApiEndpoint("GET", "character/{id:int}")]
    public async Task<IResult> GetCharacterReportsAsync(
        HttpContext context,
        int id,
        CancellationToken cancellationToken)
    {
        if (await TryApplyRateLimitAsync(context, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (id <= 0)
        {
            return BadRequest("Route parameter 'id' must be a positive integer.");
        }

        var cacheKey = CacheKeys.Character(id);

        if (cache.TryGetValue(cacheKey, out CharacterReports? cached) && cached is not null)
        {
            ApplyNoStoreCacheHeaders(context.Response, hit: true);
            return Json(cached);
        }

        var result = await fellowshipLogsService.GetCharacterReportsAsync(id, cancellationToken);
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PositiveDuration(
                cacheOptions.RecentReportMetadataCacheDuration,
                TimeSpan.FromMinutes(10))
        });
        ApplyNoStoreCacheHeaders(context.Response, hit: false);

        return Json(result);
    }

    private async ValueTask<IResult?> TryApplyRateLimitAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        using var lease = await rateLimiter.AcquireAsync(partitionKey, cancellationToken);
        if (lease.IsAcquired)
        {
            return null;
        }

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        return Results.Json(new { error = "Rate limit exceeded." }, jsonOptions, statusCode: 429);
    }

    private IResult Json<T>(T value) => Results.Json(value, jsonOptions);

    private static IResult BadRequest(string message) =>
        Results.Json(new { error = message }, statusCode: 400);

    private void ApplyAnalysisPreloadCacheHeaders(HttpResponse response, AnalysisPreload preload, bool hit)
    {
        ApplyPublicCacheHeaders(response, GetAnalysisPreloadCacheDuration(preload, cacheOptions), hit);
    }

    private void ApplyCompletedEventsCacheHeaders(HttpResponse response, bool hit)
    {
        ApplyPublicCacheHeaders(
            response,
            PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30)),
            hit);
    }

    private static void ApplyPublicCacheHeaders(HttpResponse response, TimeSpan duration, bool hit)
    {
        response.Headers["Cache-Control"] = $"public, max-age={(int)duration.TotalSeconds}";
        response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static void ApplyNoStoreCacheHeaders(HttpResponse response, bool hit)
    {
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static MemoryCacheEntryOptions CreateAnalysisPreloadCacheEntryOptions(
        AnalysisPreload preload,
        FellowshipLogsCacheOptions cacheOptions)
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetAnalysisPreloadCacheDuration(preload, cacheOptions)
        };
    }

    private static MemoryCacheEntryOptions CreateCompletedEventsCacheEntryOptions(FellowshipLogsCacheOptions cacheOptions)
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PositiveDuration(
                cacheOptions.CompletedEventsCacheDuration,
                TimeSpan.FromDays(30))
        };
    }

    private static TimeSpan GetAnalysisPreloadCacheDuration(
        AnalysisPreload preload,
        FellowshipLogsCacheOptions cacheOptions)
    {
        if (preload.ReportInfo.Fights.Any(fight => fight.InProgress)
            || ReportEndedRecently(preload.ReportInfo, cacheOptions.RecentReportWindow))
        {
            return PositiveDuration(cacheOptions.RecentReportMetadataCacheDuration, TimeSpan.FromMinutes(10));
        }

        return PositiveDuration(cacheOptions.StableReportMetadataCacheDuration, TimeSpan.FromDays(30));
    }

    private static bool ReportEndedRecently(ReportInfo reportInfo, TimeSpan recentWindow)
    {
        if (reportInfo.EndTime is null)
        {
            return true;
        }

        var reportEnd = TryReadUnixTimestamp(reportInfo.EndTime.Value);
        if (reportEnd is null)
        {
            return true;
        }

        var window = PositiveDuration(recentWindow, TimeSpan.FromHours(2));
        return DateTimeOffset.UtcNow - reportEnd.Value < window;
    }

    private static DateTimeOffset? TryReadUnixTimestamp(double value)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        try
        {
            var milliseconds = value < 10_000_000_000 ? value * 1000 : value;
            return DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static TimeSpan PositiveDuration(TimeSpan value, TimeSpan fallback)
    {
        return value > TimeSpan.Zero ? value : fallback;
    }

    private static string GetRateLimitPartitionKey(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var forwardedClient = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedClient))
            {
                return forwardedClient;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
