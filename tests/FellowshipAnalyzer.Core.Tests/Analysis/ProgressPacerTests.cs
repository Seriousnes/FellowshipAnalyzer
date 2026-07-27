using System.Diagnostics;

using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class ProgressPacerTests
{
    private const int PastTheIntervalMs = 1000 / ProgressPacer.UpdatesPerSecond * 3;

    /// <summary>
    /// Deliberately asserted after the interval has elapsed, so the mask is the only thing that can be
    /// holding the yield back and no amount of scheduling delay can change the outcome.
    /// </summary>
    [Fact]
    public void ShouldYield_IgnoresIterationsOffTheCheckInterval()
    {
        var pacer = new ProgressPacer();
        Thread.Sleep(PastTheIntervalMs);

        for (var iteration = 1; iteration < ProgressPacer.CheckInterval; iteration++)
        {
            pacer.ShouldYield(iteration).ShouldBeFalse($"iteration {iteration} is not on the check interval");
        }
    }

    [Fact]
    public void ShouldYield_DoesNotYieldBeforeTheIntervalElapses()
    {
        var pacer = new ProgressPacer();

        pacer.ShouldYield(0).ShouldBeFalse();
        pacer.ShouldYield(ProgressPacer.CheckInterval).ShouldBeFalse();
    }

    [Fact]
    public void ShouldYield_YieldsOnTheFirstCheckIntervalAfterTheIntervalElapses()
    {
        var pacer = new ProgressPacer();
        Thread.Sleep(PastTheIntervalMs);

        pacer.ShouldYield(ProgressPacer.CheckInterval - 1).ShouldBeFalse();
        pacer.ShouldYield(ProgressPacer.CheckInterval).ShouldBeTrue();
    }

    [Fact]
    public void ShouldYield_ArmsTheNextIntervalAfterYielding()
    {
        var pacer = new ProgressPacer();
        Thread.Sleep(PastTheIntervalMs);

        pacer.ShouldYield(0).ShouldBeTrue();
        pacer.ShouldYield(ProgressPacer.CheckInterval).ShouldBeFalse();
    }

    /// <summary>
    /// The point of pacing by time is that yields track how long a loop runs, not how many iterations it
    /// has, so a report with ten times the events does not pay ten times the re-render cost. The upper
    /// bound is what makes that true and holds however fast the machine runs the loop.
    /// </summary>
    [Fact]
    public void ShouldYield_StaysWithinThePublishRateHoweverManyIterationsRun()
    {
        const int WindowMs = 300;

        var pacer = new ProgressPacer();
        var elapsed = Stopwatch.StartNew();

        var yields = 0;
        var iterations = 0;
        while (elapsed.ElapsedMilliseconds < WindowMs)
        {
            if (pacer.ShouldYield(iterations))
                yields++;
            iterations++;
        }

        iterations.ShouldBeGreaterThan(ProgressPacer.CheckInterval * 100);
        yields.ShouldBeLessThanOrEqualTo(WindowMs * ProgressPacer.UpdatesPerSecond / 1000 + 1);
        yields.ShouldBeLessThan(iterations / ProgressPacer.CheckInterval);
    }
}
