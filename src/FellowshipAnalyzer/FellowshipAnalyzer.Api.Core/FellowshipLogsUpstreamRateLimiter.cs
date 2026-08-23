using System.Threading.RateLimiting;

namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Single global fixed-window limiter guarding the upstream Fellowship Logs GraphQL calls. Unlike
/// <see cref="FellowshipLogsRateLimiter"/> it is not partitioned by caller identity, so it cannot be
/// bypassed by rotating source addresses; it exists to bound total upstream cost (FellowshipLogs
/// quota, compute, blob writes) on the cache-miss path.
/// </summary>
public sealed class FellowshipLogsUpstreamRateLimiter : IDisposable
{
    private readonly FixedWindowRateLimiter _limiter;

    public FellowshipLogsUpstreamRateLimiter(FellowshipLogsUpstreamRateLimitOptions options)
    {
        _limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = Math.Max(1, options.PermitLimit),
            QueueLimit = Math.Max(0, options.QueueLimit),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = options.Window > TimeSpan.Zero ? options.Window : TimeSpan.FromMinutes(1),
        });
    }

    /// <summary>
    /// Attempts to take a permit without queuing. Over-limit callers get an immediately
    /// non-acquired lease (with <c>RetryAfter</c>) rather than blocking a request thread and an
    /// Azure Function invocation for up to a full window.
    /// </summary>
    public RateLimitLease AttemptAcquire() => _limiter.AttemptAcquire(permitCount: 1);

    public void Dispose() => _limiter.Dispose();
}
