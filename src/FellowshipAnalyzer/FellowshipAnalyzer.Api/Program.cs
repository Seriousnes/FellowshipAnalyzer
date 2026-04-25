using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.Extensions;
using FellowshipAnalyzer.ServiceDefaults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

const string WasmHostCorsPolicy = "WasmHost";
const string FellowshipLogsApiRateLimitPolicy = "FellowshipLogsApi";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFellowshipLogsService(builder.Configuration);
builder.Services.AddMemoryCache();

var cacheOptions = builder.Configuration
    .GetSection(FellowshipLogsProxyCacheOptions.SectionName)
    .Get<FellowshipLogsProxyCacheOptions>()
    ?? new FellowshipLogsProxyCacheOptions();
var rateLimitOptions = builder.Configuration
    .GetSection(FellowshipLogsProxyRateLimitOptions.SectionName)
    .Get<FellowshipLogsProxyRateLimitOptions>()
    ?? new FellowshipLogsProxyRateLimitOptions();

builder.Services.AddSingleton(cacheOptions);
builder.Services.AddSingleton(rateLimitOptions);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded." },
            cancellationToken);
    };

    options.AddPolicy(
        FellowshipLogsApiRateLimitPolicy,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = Math.Max(1, rateLimitOptions.PermitLimit),
                QueueLimit = Math.Max(0, rateLimitOptions.QueueLimit),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = rateLimitOptions.Window > TimeSpan.Zero
                    ? rateLimitOptions.Window
                    : TimeSpan.FromMinutes(1)
            }));
});

var configuredAllowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        WasmHostCorsPolicy,
        policy =>
        {
            policy.AllowAnyHeader()
                .AllowAnyMethod();

            if (configuredAllowedOrigins.Length > 0)
            {
                policy.WithOrigins(configuredAllowedOrigins);
                return;
            }

            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(IsDevelopmentLoopbackOrigin);
                return;
            }
        });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
UseSecurityHeaders(app);
app.UseCors();
app.UseRateLimiter();

app.MapGet(
        "/api/events",
        async (
            string reportCode,
            int playerId,
            int fightId,
            IFellowshipLogsClient client,
            IMemoryCache cache,
            FellowshipLogsProxyCacheOptions cacheOptions,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var request = new FellowshipLogsEventsRequest(reportCode, playerId, fightId);
            var cacheKey = CacheKeys.Events(request);

            if (cache.TryGetValue(cacheKey, out RawEventsResponse? cachedResult) && cachedResult is not null)
            {
                SetCompletedEventsCacheHeaders(httpContext, cacheOptions, hit: true);
                return Results.Bytes(cachedResult.JsonBytes, "application/json");
            }

            var result = await client.Events.GetRawBytesAsync(request, cancellationToken);
            var inProgress = result.InProgress
                ?? throw new InvalidOperationException("Raw events response did not include fight progress state.");

            if (!inProgress)
            {
                cache.Set(cacheKey, result, CreateCompletedEventsCacheEntryOptions(cacheOptions));
                SetCompletedEventsCacheHeaders(httpContext, cacheOptions, hit: false);
            }
            else
            {
                SetNoStoreCacheHeaders(httpContext, hit: false);
            }

            return Results.Bytes(result.JsonBytes, "application/json");
        })
    .RequireCors(WasmHostCorsPolicy)
    .RequireRateLimiting(FellowshipLogsApiRateLimitPolicy);

app.MapGet(
        "/api/analysis/{reportCode}",
        async (
            string reportCode,
            IFellowshipLogsClient client,
            IMemoryCache cache,
            FellowshipLogsProxyCacheOptions cacheOptions,
            HttpContext httpContext,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var cacheKey = CacheKeys.AnalysisPreload(reportCode);

            if (cache.TryGetValue(cacheKey, out AnalysisPreload? cachedPreload) && cachedPreload is not null)
            {
                SetAnalysisPreloadCacheHeaders(httpContext, cachedPreload, cacheOptions, hit: true);
                return Results.Json(cachedPreload, jsonOptions);
            }

            var preload = await client.AnalysisPreload.GetAsync(reportCode, cancellationToken);

            cache.Set(cacheKey, preload, CreateAnalysisPreloadCacheEntryOptions(preload, cacheOptions));
            SetAnalysisPreloadCacheHeaders(httpContext, preload, cacheOptions, hit: false);

            return Results.Json(preload, jsonOptions);
        })
    .RequireCors(WasmHostCorsPolicy)
    .RequireRateLimiting(FellowshipLogsApiRateLimitPolicy);

app.Run();

static MemoryCacheEntryOptions CreateAnalysisPreloadCacheEntryOptions(
    AnalysisPreload preload,
    FellowshipLogsProxyCacheOptions cacheOptions)
{
    return new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = GetAnalysisPreloadCacheDuration(preload, cacheOptions)
    };
}

static MemoryCacheEntryOptions CreateCompletedEventsCacheEntryOptions(FellowshipLogsProxyCacheOptions cacheOptions)
{
    return new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = PositiveDuration(
            cacheOptions.CompletedEventsCacheDuration,
            TimeSpan.FromDays(30))
    };
}

static TimeSpan GetAnalysisPreloadCacheDuration(
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

static bool ReportEndedRecently(ReportInfo reportInfo, TimeSpan recentWindow)
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

static DateTimeOffset? TryReadUnixTimestamp(double value)
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

static TimeSpan PositiveDuration(TimeSpan value, TimeSpan fallback)
{
    return value > TimeSpan.Zero ? value : fallback;
}

static void SetAnalysisPreloadCacheHeaders(
    HttpContext httpContext,
    AnalysisPreload preload,
    FellowshipLogsProxyCacheOptions cacheOptions,
    bool hit)
{
    SetPublicCacheHeaders(httpContext, GetAnalysisPreloadCacheDuration(preload, cacheOptions), hit);
}

static void SetCompletedEventsCacheHeaders(
    HttpContext httpContext,
    FellowshipLogsProxyCacheOptions cacheOptions,
    bool hit)
{
    SetPublicCacheHeaders(
        httpContext,
        PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30)),
        hit);
}

static void SetPublicCacheHeaders(HttpContext httpContext, TimeSpan duration, bool hit)
{
    httpContext.Response.Headers["Cache-Control"] = $"public, max-age={(int)duration.TotalSeconds}";
    httpContext.Response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
}

static void SetNoStoreCacheHeaders(HttpContext httpContext, bool hit)
{
    httpContext.Response.Headers["Cache-Control"] = "no-store";
    httpContext.Response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
}

static string GetRateLimitPartitionKey(HttpContext httpContext)
{
    var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        var forwardedClient = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedClient))
        {
            return forwardedClient;
        }
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static void UseSecurityHeaders(IApplicationBuilder app)
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        await next(context);
    });
}

static bool IsDevelopmentLoopbackOrigin(string origin)
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

internal sealed class FellowshipLogsProxyCacheOptions
{
    public const string SectionName = "FellowshipLogsProxy:Cache";

    public TimeSpan RecentReportWindow { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan RecentReportMetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan StableReportMetadataCacheDuration { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan CompletedEventsCacheDuration { get; set; } = TimeSpan.FromDays(30);
}

internal sealed class FellowshipLogsProxyRateLimitOptions
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
