using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using FellowshipAnalyzer.Core.FellowshipLogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;

namespace FellowshipAnalyzer.Api;

public sealed class FellowshipLogsProxyFunctions(
    IFellowshipLogsClient client,
    IMemoryCache cache,
    FellowshipLogsProxyCacheOptions cacheOptions,
    FellowshipLogsProxyRateLimiter rateLimiter,
    FellowshipLogsProxyCorsOptions corsOptions,
    JsonSerializerOptions jsonOptions)
{
    [Function(nameof(GetEvents))]
    public async Task<IActionResult> GetEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        PrepareResponse(request);

        var rateLimitResult = await TryApplyRateLimitAsync(request, cancellationToken);
        if (rateLimitResult is not null)
        {
            return rateLimitResult;
        }

        if (!TryReadRequiredQueryString(request, "reportCode", out var reportCode, out var reportCodeError))
        {
            return reportCodeError;
        }

        if (!TryReadRequiredQueryInt(request, "playerId", out var playerId, out var playerIdError))
        {
            return playerIdError;
        }

        if (!TryReadRequiredQueryInt(request, "fightId", out var fightId, out var fightIdError))
        {
            return fightIdError;
        }

        var fellowshipLogsRequest = new FellowshipLogsEventsRequest(reportCode, playerId, fightId);
        var cacheKey = CacheKeys.Events(fellowshipLogsRequest);

        if (cache.TryGetValue(cacheKey, out RawEventsResponse? cachedResult) && cachedResult is not null)
        {
            SetCompletedEventsCacheHeaders(request.HttpContext, cacheOptions, hit: true);
            return new FileContentResult(cachedResult.JsonBytes, "application/json");
        }

        var result = await client.Events.GetRawBytesAsync(fellowshipLogsRequest, cancellationToken);
        var inProgress = result.InProgress
            ?? throw new InvalidOperationException("Raw events response did not include fight progress state.");

        if (!inProgress)
        {
            cache.Set(cacheKey, result, CreateCompletedEventsCacheEntryOptions(cacheOptions));
            SetCompletedEventsCacheHeaders(request.HttpContext, cacheOptions, hit: false);
        }
        else
        {
            SetNoStoreCacheHeaders(request.HttpContext, hit: false);
        }

        return new FileContentResult(result.JsonBytes, "application/json");
    }

    [Function(nameof(GetAnalysis))]
    public async Task<IActionResult> GetAnalysis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "analysis/{reportCode}")] HttpRequest request,
        string reportCode,
        CancellationToken cancellationToken)
    {
        PrepareResponse(request);

        var rateLimitResult = await TryApplyRateLimitAsync(request, cancellationToken);
        if (rateLimitResult is not null)
        {
            return rateLimitResult;
        }

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest("Route parameter 'reportCode' is required.");
        }

        var cacheKey = CacheKeys.AnalysisPreload(reportCode);

        if (cache.TryGetValue(cacheKey, out AnalysisPreload? cachedPreload) && cachedPreload is not null)
        {
            SetAnalysisPreloadCacheHeaders(request.HttpContext, cachedPreload, cacheOptions, hit: true);
            return Json(cachedPreload);
        }

        var preload = await client.AnalysisPreload.GetAsync(reportCode, cancellationToken);

        cache.Set(cacheKey, preload, CreateAnalysisPreloadCacheEntryOptions(preload, cacheOptions));
        SetAnalysisPreloadCacheHeaders(request.HttpContext, preload, cacheOptions, hit: false);

        return Json(preload);
    }

    private async ValueTask<IActionResult?> TryApplyRateLimitAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(GetRateLimitPartitionKey(request), cancellationToken);
        if (lease.IsAcquired)
        {
            return null;
        }

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            request.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Rate limit exceeded." });
    }

    private void PrepareResponse(HttpRequest request)
    {
        var responseHeaders = request.HttpContext.Response.Headers;
        responseHeaders.TryAdd("X-Content-Type-Options", "nosniff");
        responseHeaders.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

        var origin = request.Headers.Origin.ToString();
        if (corsOptions.IsAllowedOrigin(origin))
        {
            responseHeaders["Access-Control-Allow-Origin"] = origin;
            responseHeaders["Vary"] = "Origin";
        }
    }

    private ContentResult Json<T>(T value)
    {
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(value, jsonOptions),
            ContentType = "application/json",
            StatusCode = StatusCodes.Status200OK
        };
    }

    private static MemoryCacheEntryOptions CreateAnalysisPreloadCacheEntryOptions(
        AnalysisPreload preload,
        FellowshipLogsProxyCacheOptions cacheOptions)
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GetAnalysisPreloadCacheDuration(preload, cacheOptions)
        };
    }

    private static MemoryCacheEntryOptions CreateCompletedEventsCacheEntryOptions(FellowshipLogsProxyCacheOptions cacheOptions)
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
        FellowshipLogsProxyCacheOptions cacheOptions)
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

    private static void SetAnalysisPreloadCacheHeaders(
        HttpContext httpContext,
        AnalysisPreload preload,
        FellowshipLogsProxyCacheOptions cacheOptions,
        bool hit)
    {
        SetPublicCacheHeaders(httpContext, GetAnalysisPreloadCacheDuration(preload, cacheOptions), hit);
    }

    private static void SetCompletedEventsCacheHeaders(
        HttpContext httpContext,
        FellowshipLogsProxyCacheOptions cacheOptions,
        bool hit)
    {
        SetPublicCacheHeaders(
            httpContext,
            PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30)),
            hit);
    }

    private static void SetPublicCacheHeaders(HttpContext httpContext, TimeSpan duration, bool hit)
    {
        httpContext.Response.Headers["Cache-Control"] = $"public, max-age={(int)duration.TotalSeconds}";
        httpContext.Response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static void SetNoStoreCacheHeaders(HttpContext httpContext, bool hit)
    {
        httpContext.Response.Headers["Cache-Control"] = "no-store";
        httpContext.Response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
    }

    private static string GetRateLimitPartitionKey(HttpRequest request)
    {
        var forwardedFor = request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var forwardedClient = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedClient))
            {
                return forwardedClient;
            }
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool TryReadRequiredQueryString(
        HttpRequest request,
        string name,
        out string value,
        out IActionResult errorResult)
    {
        value = request.Query[name].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            errorResult = null!;
            return true;
        }

        errorResult = BadRequest($"Missing required query parameter '{name}'.");
        return false;
    }

    private static bool TryReadRequiredQueryInt(
        HttpRequest request,
        string name,
        out int value,
        out IActionResult errorResult)
    {
        var rawValue = request.Query[name].ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = 0;
            errorResult = BadRequest($"Missing required query parameter '{name}'.");
            return false;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            errorResult = null!;
            return true;
        }

        errorResult = BadRequest($"Query parameter '{name}' must be an integer.");
        return false;
    }

    private static IActionResult BadRequest(string message)
    {
        return new BadRequestObjectResult(new { error = message });
    }

    private static IActionResult StatusCode(int statusCode, object value)
    {
        return new ObjectResult(value)
        {
            StatusCode = statusCode
        };
    }
}

public sealed class FellowshipLogsProxyRateLimiter(FellowshipLogsProxyRateLimitOptions options) : IDisposable
{
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = [];

    public ValueTask<RateLimitLease> AcquireAsync(string partitionKey, CancellationToken cancellationToken)
    {
        var limiter = _limiters.GetOrAdd(partitionKey, _ => new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = Math.Max(1, options.PermitLimit),
                QueueLimit = Math.Max(0, options.QueueLimit),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = options.Window > TimeSpan.Zero
                    ? options.Window
                    : TimeSpan.FromMinutes(1)
            }));

        return limiter.AcquireAsync(permitCount: 1, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}

public sealed class FellowshipLogsProxyCorsOptions(string[] allowedOrigins, bool allowDevelopmentLoopbackOrigins)
{
    private readonly HashSet<string> _allowedOrigins = allowedOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsAllowedOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        return _allowedOrigins.Contains(origin)
            || (allowDevelopmentLoopbackOrigins && IsDevelopmentLoopbackOrigin(origin));
    }

    private static bool IsDevelopmentLoopbackOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FellowshipLogsProxyCacheOptions
{
    public const string SectionName = "FellowshipLogsProxy:Cache";

    public TimeSpan RecentReportWindow { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan RecentReportMetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan StableReportMetadataCacheDuration { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan CompletedEventsCacheDuration { get; set; } = TimeSpan.FromDays(30);
}

public sealed class FellowshipLogsProxyRateLimitOptions
{
    public const string SectionName = "FellowshipLogsProxy:RateLimit";

    public int PermitLimit { get; set; } = 60;
    public int QueueLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

internal static class CacheKeys
{
    public static string AnalysisPreload(string reportCode) => $"analysis:{reportCode.Trim()}";

    public static string Events(FellowshipLogsEventsRequest request)
    {
        return $"events:{request.ReportCode.Trim()}:{request.FightId}:{request.PlayerId}";
    }
}
