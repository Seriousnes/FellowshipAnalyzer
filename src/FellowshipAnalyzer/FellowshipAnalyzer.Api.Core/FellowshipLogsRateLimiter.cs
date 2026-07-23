using System.Threading.RateLimiting;

namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Per-client fixed-window rate limiter. Backed by <see cref="PartitionedRateLimiter"/> so idle
/// partitions are garbage-collected automatically; a hand-rolled dictionary would retain one
/// limiter per distinct partition key for the lifetime of the process.
/// </summary>
public sealed class FellowshipLogsRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public FellowshipLogsRateLimiter(FellowshipLogsRateLimitOptions options)
    {
        var permitLimit = Math.Max(1, options.PermitLimit);
        var queueLimit = Math.Max(0, options.QueueLimit);
        var window = options.Window > TimeSpan.Zero ? options.Window : TimeSpan.FromMinutes(1);

        _limiter = PartitionedRateLimiter.Create<string, string>(partitionKey =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = window,
                }));
    }

    public ValueTask<RateLimitLease> AcquireAsync(string partitionKey, CancellationToken cancellationToken)
        => _limiter.AcquireAsync(partitionKey, permitCount: 1, cancellationToken);

    public void Dispose() => _limiter.Dispose();
}
