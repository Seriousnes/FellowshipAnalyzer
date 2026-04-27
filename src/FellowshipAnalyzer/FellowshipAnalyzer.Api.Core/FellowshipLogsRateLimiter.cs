using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace FellowshipAnalyzer.Api.Core;

public sealed class FellowshipLogsRateLimiter(FellowshipLogsRateLimitOptions options) : IDisposable
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
