using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.RateLimiting;

using FellowshipAnalyzer.Api.Core.Caching;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

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
    JsonSerializerOptions jsonOptions,
    IPersistentCache persistentCache,
    RecyclableMemoryStreamManager streamManager,
    IHostApplicationLifetime appLifetime,
    ILogger<FellowshipLogsApiHandler> logger)
{
    [ApiEndpoint("GET", "events")]
    public async Task<IResult> GetEventsAsync(
        HttpContext context,
        string? reportCode,
        int? playerId,
        int? fightId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "GetEventsAsync ENTER reportCode={ReportCode} playerId={PlayerId} fightId={FightId}",
            reportCode, playerId, fightId);

        if (await TryApplyRateLimitAsync(context, cancellationToken) is { } limited)
        {
            logger.LogWarning("GetEventsAsync rate-limited at {ElapsedMs}ms", sw.ElapsedMilliseconds);
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

        // L1 skipped for events — payloads too large for in-process cache.

        // L2: Blob cache
        var blobKey = CacheKeys.BlobEvents(trimmedReportCode, playerId.Value, fightId.Value);
        logger.LogInformation("GetEventsAsync L2 lookup blobKey={BlobKey} t={ElapsedMs}ms", blobKey, sw.ElapsedMilliseconds);
        var blobEntry = await persistentCache.GetAsync(CachePartition.Events, blobKey, cancellationToken);
        logger.LogInformation(
            "GetEventsAsync L2 lookup result hit={Hit} encoding={Encoding} length={Length} t={ElapsedMs}ms",
            blobEntry is not null,
            blobEntry?.ContentEncoding,
            blobEntry?.ContentLength,
            sw.ElapsedMilliseconds);

        if (blobEntry is not null)
        {
            ApplyCompletedEventsCacheHeaders(context.Response, blobEntry.ExpiresAt, hit: true);
            // Blob is gzip-compressed for storage efficiency. Decompress on the wire so
            // clients (Swagger UI, browsers, the WASM HttpClient) receive plain JSON without
            // having to handle a custom transport encoding.
            Stream payload = blobEntry.Content;
            if (string.Equals(blobEntry.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                payload = new GZipStream(blobEntry.Content, CompressionMode.Decompress, leaveOpen: false);
            }
            logger.LogInformation("GetEventsAsync returning HIT stream at t={ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Stream(payload, "application/json");
        }

        // L3: Upstream
        logger.LogInformation("GetEventsAsync L3 upstream call starting t={ElapsedMs}ms", sw.ElapsedMilliseconds);
        var result = await fellowshipLogsService.GetRawEventsAsync(
            trimmedReportCode, playerId.Value, fightId.Value, cancellationToken);
        logger.LogInformation(
            "GetEventsAsync L3 upstream returned bytes={Bytes} inProgress={InProgress} t={ElapsedMs}ms",
            result.JsonBytes.Length, result.InProgress, sw.ElapsedMilliseconds);

        if (!result.InProgress && result.HasEvents)
        {
            var duration = PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30));
            var expiresAt = DateTimeOffset.UtcNow.Add(duration);

            // Compress JSON to gzip and upload to blob (fire-and-forget — don't delay client).
            var gzipBytes = CompressGzip(result.JsonBytes, streamManager);
            logger.LogInformation(
                "GetEventsAsync compressed for blob raw={Raw} gzip={Gzip} t={ElapsedMs}ms",
                result.JsonBytes.Length, gzipBytes.Length, sw.ElapsedMilliseconds);

            // Fire-and-forget write-through must outlive the request, so it is scoped to the
            // application lifetime rather than the request's CancellationToken (which is cancelled
            // during response teardown and would silently abort the upload).
            WriteThroughInBackground(
                persistentCache.SetAsync(
                    CachePartition.Events, blobKey,
                    gzipBytes,
                    new PersistentCacheWriteOptions(
                        ExpiresAt: expiresAt,
                        ContentType: "application/json",
                        ContentEncoding: "gzip"),
                    appLifetime.ApplicationStopping),
                CachePartition.Events, blobKey);

            ApplyCompletedEventsCacheHeaders(context.Response, expiresAt, hit: false);
            logger.LogInformation("GetEventsAsync returning MISS bytes (completed) at t={ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Bytes(result.JsonBytes, "application/json");
        }
        else
        {
            ApplyNoStoreCacheHeaders(context.Response, hit: false);
            logger.LogInformation(
                "GetEventsAsync returning MISS bytes (not cached: inProgress={InProgress} hasEvents={HasEvents}) at t={ElapsedMs}ms",
                result.InProgress, result.HasEvents, sw.ElapsedMilliseconds);
            return Results.Bytes(result.JsonBytes, "application/json");
        }
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

        // L1: In-process cache
        if (cache.TryGetValue(cacheKey, out AnalysisPreload? cachedPreload) && cachedPreload is not null)
        {
            ApplyAnalysisPreloadCacheHeaders(context.Response, cachedPreload, hit: true);
            return Json(cachedPreload);
        }

        // L2: Blob cache
        var blobKey = CacheKeys.BlobAnalysis(reportCode);
        var blobEntry = await persistentCache.GetAsync(CachePartition.Metadata, blobKey, cancellationToken);
        if (blobEntry is not null)
        {
            var blobPreload = await JsonSerializer.DeserializeAsync<AnalysisPreload>(
                blobEntry.Content, jsonOptions, cancellationToken);
            await blobEntry.Content.DisposeAsync();

            if (blobPreload is not null)
            {
                cache.Set(cacheKey, blobPreload, CreateAnalysisPreloadCacheEntryOptions(blobPreload, cacheOptions));
                ApplyAnalysisPreloadCacheHeaders(context.Response, blobPreload, hit: true);
                return Json(blobPreload);
            }
        }

        // L3: Upstream
        var preload = await fellowshipLogsService.GetReportMasterDataAsync(reportCode, cancellationToken);
        var analysisDuration = GetAnalysisPreloadCacheDuration(preload, cacheOptions);
        var analysisExpiresAt = DateTimeOffset.UtcNow.Add(analysisDuration);

        cache.Set(cacheKey, preload, CreateAnalysisPreloadCacheEntryOptions(preload, cacheOptions));

        // Write-through to L2 (fire-and-forget). Scoped to the application lifetime, not the
        // request token, so the upload is not aborted when the response completes.
        var preloadBytes = JsonSerializer.SerializeToUtf8Bytes(preload, jsonOptions);
        WriteThroughInBackground(
            persistentCache.SetAsync(
                CachePartition.Metadata, blobKey,
                preloadBytes,
                new PersistentCacheWriteOptions(
                    ExpiresAt: analysisExpiresAt,
                    ContentType: "application/json",
                    ContentEncoding: null),
                appLifetime.ApplicationStopping),
            CachePartition.Metadata, blobKey);

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

        // L1: In-process cache
        if (cache.TryGetValue(cacheKey, out CharacterReports? cached) && cached is not null)
        {
            ApplyNoStoreCacheHeaders(context.Response, hit: true);
            return Json(cached);
        }

        // L2: Blob cache
        var blobKey = CacheKeys.BlobCharacter(id);
        var blobEntry = await persistentCache.GetAsync(CachePartition.Metadata, blobKey, cancellationToken);
        if (blobEntry is not null)
        {
            var blobReports = await JsonSerializer.DeserializeAsync<CharacterReports>(
                blobEntry.Content, jsonOptions, cancellationToken);
            await blobEntry.Content.DisposeAsync();

            if (blobReports is not null)
            {
                var characterDuration = PositiveDuration(
                    cacheOptions.RecentReportMetadataCacheDuration,
                    TimeSpan.FromMinutes(10));
                cache.Set(cacheKey, blobReports, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = characterDuration
                });
                ApplyNoStoreCacheHeaders(context.Response, hit: true);
                return Json(blobReports);
            }
        }

        // L3: Upstream
        var result = await fellowshipLogsService.GetCharacterReportsAsync(id, cancellationToken);
        var charDuration = PositiveDuration(
            cacheOptions.RecentReportMetadataCacheDuration,
            TimeSpan.FromMinutes(10));
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = charDuration
        });


        var resultBytes = JsonSerializer.SerializeToUtf8Bytes(result, jsonOptions);
        await persistentCache.SetAsync(
            CachePartition.Metadata, blobKey,
            resultBytes,
            new PersistentCacheWriteOptions(
                ExpiresAt: DateTimeOffset.UtcNow.Add(charDuration),
                ContentType: "application/json",
                ContentEncoding: null),
            cancellationToken);

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
            context.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return Results.Json(new { error = "Rate limit exceeded." }, jsonOptions, statusCode: 429);
    }

    private IResult Json<T>(T value) => Results.Json(value, jsonOptions);

    private static IResult BadRequest(string message) =>
        Results.Json(new { error = message }, statusCode: 400);

    /// <summary>
    /// Observes an already-started persistent-cache write-through without blocking the response.
    /// Failures are logged rather than silently discarded; the write itself is scoped to the
    /// application lifetime by the caller so it survives request teardown.
    /// </summary>
    private void WriteThroughInBackground(ValueTask writeOperation, CachePartition partition, string key)
    {
        _ = ObserveWriteThroughAsync(writeOperation, partition, key);
    }

    private async Task ObserveWriteThroughAsync(ValueTask writeOperation, CachePartition partition, string key)
    {
        try
        {
            await writeOperation;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Persistent cache write-through failed partition={Partition} key={Key}.",
                partition, key);
        }
    }

    private void ApplyAnalysisPreloadCacheHeaders(HttpResponse response, AnalysisPreload preload, bool hit)
    {
        var duration = GetAnalysisPreloadCacheDuration(preload, cacheOptions);
        ApplyPublicCacheHeaders(response, duration, DateTimeOffset.UtcNow.Add(duration), hit);
    }

    private void ApplyCompletedEventsCacheHeaders(HttpResponse response, DateTimeOffset? expiresAt, bool hit)
    {
        var duration = PositiveDuration(cacheOptions.CompletedEventsCacheDuration, TimeSpan.FromDays(30));
        ApplyPublicCacheHeaders(response, duration, expiresAt ?? DateTimeOffset.UtcNow.Add(duration), hit);
    }

    private static void ApplyPublicCacheHeaders(HttpResponse response, TimeSpan duration, DateTimeOffset expiresAt, bool hit)
    {
        response.Headers.CacheControl = $"public, max-age={(int)duration.TotalSeconds}";
        response.Headers["X-FellowshipAnalyzer-Cache"] = hit ? "HIT" : "MISS";
        response.Headers["X-FellowshipAnalyzer-ExpiresAt"] = expiresAt.ToString("O");
    }

    private static void ApplyNoStoreCacheHeaders(HttpResponse response, bool hit)
    {
        response.Headers.CacheControl = "no-store";
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

    /// <summary>
    /// Compresses <paramref name="input"/> bytes using gzip (Optimal level).
    /// Uses <see cref="RecyclableMemoryStreamManager"/> to pool the intermediate buffer.
    /// </summary>
    private static byte[] CompressGzip(byte[] input, RecyclableMemoryStreamManager mgr)
    {
        using var output = mgr.GetStream("gzip-compress");
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            if (MemoryMarshal.TryGetArray<byte>(input, out var seg))
            {
                gzip.Write(seg.Array!, seg.Offset, seg.Count);
            }
            else
            {
                gzip.Write(input, 0, input.Length);
            }
        }
        return output.ToArray();
    }
}

