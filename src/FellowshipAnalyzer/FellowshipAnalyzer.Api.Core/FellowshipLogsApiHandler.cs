using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Caching.Memory;

namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Framework-agnostic implementation of the FellowshipLogs API endpoints: rate limiting, in-memory caching,
/// CORS evaluation, and security headers. Hosts translate <see cref="EndpointResponse"/> into the
/// native response type for their HTTP framework (Functions, Kestrel, etc.).
/// </summary>
public sealed class FellowshipLogsApiHandler(
    FellowshipLogsService fellowshipLogsService,
    IMemoryCache cache,
    FellowshipLogsCacheOptions cacheOptions,
    FellowshipLogsRateLimiter rateLimiter,
    FellowshipLogsCorsOptions corsOptions,
    JsonSerializerOptions jsonOptions)
{
    public async Task<EndpointResponse> GetEventsAsync(
        EndpointRequestContext requestContext,
        string? reportCode,
        int? playerId,
        int? fightId,
        CancellationToken cancellationToken)
    {
        var response = StartResponse(requestContext);

        if (await TryApplyRateLimitAsync(response, requestContext, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest(response, "Missing required query parameter 'reportCode'.");
        }
        if (playerId is null)
        {
            return BadRequest(response, "Missing required query parameter 'playerId'.");
        }
        if (fightId is null)
        {
            return BadRequest(response, "Missing required query parameter 'fightId'.");
        }

        var trimmedReportCode = reportCode.Trim();
        var cacheKey = CacheKeys.Events(trimmedReportCode, playerId.Value, fightId.Value);

        if (cache.TryGetValue(cacheKey, out RawEventsResult? cachedResult) && cachedResult is not null)
        {
            ApplyCompletedEventsCacheHeaders(response, hit: true);
            return Ok(response, cachedResult.JsonBytes, "application/json");
        }

        var result = await fellowshipLogsService.GetRawEventsAsync(
            trimmedReportCode, playerId.Value, fightId.Value, cancellationToken);

        if (!result.InProgress)
        {
            cache.Set(cacheKey, result, CreateCompletedEventsCacheEntryOptions(cacheOptions));
            ApplyCompletedEventsCacheHeaders(response, hit: false);
        }
        else
        {
            ApplyNoStoreCacheHeaders(response, hit: false);
        }

        return Ok(response, result.JsonBytes, "application/json");
    }

    public async Task<EndpointResponse> GetAnalysisAsync(
        EndpointRequestContext requestContext,
        string reportCode,
        CancellationToken cancellationToken)
    {
        var response = StartResponse(requestContext);

        if (await TryApplyRateLimitAsync(response, requestContext, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest(response, "Route parameter 'reportCode' is required.");
        }

        var cacheKey = CacheKeys.Analysis(reportCode);

        if (cache.TryGetValue(cacheKey, out AnalysisPreloadResponse? cachedPreload) && cachedPreload is not null)
        {
            ApplyAnalysisPreloadCacheHeaders(response, cachedPreload, hit: true);
            return JsonOk(response, cachedPreload);
        }

        var preload = await fellowshipLogsService.GetReportMasterDataAsync(reportCode, cancellationToken);
        cache.Set(cacheKey, preload, CreateAnalysisPreloadCacheEntryOptions(preload, cacheOptions));
        ApplyAnalysisPreloadCacheHeaders(response, preload, hit: false);

        return JsonOk(response, preload);
    }

    public async Task<EndpointResponse> GetCharacterReportsAsync(
        EndpointRequestContext requestContext,
        int? characterId,
        CancellationToken cancellationToken)
    {
        var response = StartResponse(requestContext);

        if (await TryApplyRateLimitAsync(response, requestContext, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (characterId is null or <= 0)
        {
            return BadRequest(response, "Route parameter 'id' must be a positive integer.");
        }

        var cacheKey = CacheKeys.Character(characterId.Value);

        if (cache.TryGetValue(cacheKey, out CharacterReportsResponse? cached) && cached is not null)
        {
            ApplyNoStoreCacheHeaders(response, hit: true);
            return JsonOk(response, cached);
        }

        var result = await fellowshipLogsService.GetCharacterReportsAsync(characterId.Value, cancellationToken);
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PositiveDuration(
                cacheOptions.RecentReportMetadataCacheDuration,
                TimeSpan.FromMinutes(10))
        });
        ApplyNoStoreCacheHeaders(response, hit: false);

        return JsonOk(response, result);
    }

    private EndpointResponse StartResponse(EndpointRequestContext requestContext)
    {
        var response = new EndpointResponse
        {
            StatusCode = 200,
            ContentType = "application/json"
        };

        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        if (corsOptions.IsAllowedOrigin(requestContext.Origin))
        {
            response.Headers["Access-Control-Allow-Origin"] = requestContext.Origin;
            response.Headers["Vary"] = "Origin";
        }

        return response;
    }

    private async ValueTask<EndpointResponse?> TryApplyRateLimitAsync(
        EndpointResponse response,
        EndpointRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        var partitionKey = GetRateLimitPartitionKey(requestContext);
        using var lease = await rateLimiter.AcquireAsync(partitionKey, cancellationToken);
        if (lease.IsAcquired)
        {
            return null;
        }

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        return WriteJson(response, 429, new { error = "Rate limit exceeded." });
    }

    private static EndpointResponse Ok(EndpointResponse response, byte[] body, string contentType)
    {
        return new EndpointResponse
        {
            StatusCode = 200,
            ContentType = contentType,
            Body = body
        }.CopyHeadersFrom(response);
    }

    private EndpointResponse JsonOk<T>(EndpointResponse response, T value)
    {
        return WriteJson(response, 200, value);
    }

    private EndpointResponse WriteJson<T>(EndpointResponse response, int statusCode, T value)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions);
        return new EndpointResponse
        {
            StatusCode = statusCode,
            ContentType = "application/json",
            Body = body
        }.CopyHeadersFrom(response);
    }

    private static EndpointResponse BadRequest(EndpointResponse response, string message)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { error = message });
        return new EndpointResponse
        {
            StatusCode = 400,
            ContentType = "application/json",
            Body = body
        }.CopyHeadersFrom(response);
    }

    private void ApplyAnalysisPreloadCacheHeaders(EndpointResponse response, AnalysisPreloadResponse preload, bool hit)
    {
        ApplyPublicCacheHeaders(response, GetAnalysisPreloadCacheDuration(preload, cacheOptions), hit);
    }

    private void ApplyCompletedEventsCacheHeaders(EndpointResponse response, bool hit)
    {
        ApplyPublicCacheHeaders(
            response,
            PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30)),
            hit);
    }

    private static void ApplyPublicCacheHeaders(EndpointResponse response, TimeSpan duration, bool hit)
    {
        response.Headers["Cache-Control"] = $"public, max-age={(int)duration.TotalSeconds}";
        response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static void ApplyNoStoreCacheHeaders(EndpointResponse response, bool hit)
    {
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static MemoryCacheEntryOptions CreateAnalysisPreloadCacheEntryOptions(
        AnalysisPreloadResponse preload,
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
        AnalysisPreloadResponse preload,
        FellowshipLogsCacheOptions cacheOptions)
    {
        if (preload.ReportInfo.Fights.Any(fight => fight.InProgress)
            || ReportEndedRecently(preload.ReportInfo, cacheOptions.RecentReportWindow))
        {
            return PositiveDuration(cacheOptions.RecentReportMetadataCacheDuration, TimeSpan.FromMinutes(10));
        }

        return PositiveDuration(cacheOptions.StableReportMetadataCacheDuration, TimeSpan.FromDays(30));
    }

    private static bool ReportEndedRecently(ReportInfoResponse reportInfo, TimeSpan recentWindow)
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

    private static string GetRateLimitPartitionKey(EndpointRequestContext requestContext)
    {
        var forwardedFor = requestContext.ForwardedFor;
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var forwardedClient = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedClient))
            {
                return forwardedClient;
            }
        }

        return requestContext.RemoteIp ?? "unknown";
    }
}

internal static class EndpointResponseExtensions
{
    public static EndpointResponse CopyHeadersFrom(this EndpointResponse target, EndpointResponse source)
    {
        foreach (var (key, value) in source.Headers)
        {
            target.Headers[key] = value;
        }
        return target;
    }
}
