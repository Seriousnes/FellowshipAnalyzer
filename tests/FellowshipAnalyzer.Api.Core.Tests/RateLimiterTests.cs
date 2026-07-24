using FellowshipAnalyzer.Api.Core;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class RateLimiterTests
{
    [Fact]
    public void UpstreamRateLimiter_FailsFast_OncePermitLimitReached()
    {
        using var limiter = new FellowshipLogsUpstreamRateLimiter(
            new FellowshipLogsUpstreamRateLimitOptions
            {
                PermitLimit = 2,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5),
            });

        using var first = limiter.AttemptAcquire();
        using var second = limiter.AttemptAcquire();
        using var third = limiter.AttemptAcquire();

        first.IsAcquired.ShouldBeTrue();
        second.IsAcquired.ShouldBeTrue();
        third.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public async Task RateLimiter_IsPartitionedPerKey_AndRejectsPastPermitLimit()
    {
        using var limiter = new FellowshipLogsRateLimiter(
            new FellowshipLogsRateLimitOptions
            {
                PermitLimit = 2,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5),
            });

        using var a1 = await limiter.AcquireAsync("client-a", CancellationToken.None);
        using var a2 = await limiter.AcquireAsync("client-a", CancellationToken.None);
        using var a3 = await limiter.AcquireAsync("client-a", CancellationToken.None);

        a1.IsAcquired.ShouldBeTrue();
        a2.IsAcquired.ShouldBeTrue();
        a3.IsAcquired.ShouldBeFalse();

        using var b1 = await limiter.AcquireAsync("client-b", CancellationToken.None);
        b1.IsAcquired.ShouldBeTrue();
    }
}
