using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;

namespace FellowshipAnalyzer.Api.Core;

/// <summary>The 429 response every endpoint returns once a client exhausts its <see cref="FellowshipLogsRateLimiter"/> window.</summary>
internal static class RateLimitResults
{
    /// <summary>A 429 result when the client's window is exhausted; null to proceed.</summary>
    public static async ValueTask<IResult?> TryApplyAsync(
        FellowshipLogsRateLimiter rateLimiter, HttpContext context, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(PartitionKey(context), cancellationToken);
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

    private static string PartitionKey(HttpContext context)
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
