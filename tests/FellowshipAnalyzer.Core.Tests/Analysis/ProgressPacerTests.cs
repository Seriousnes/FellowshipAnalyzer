using System.Diagnostics;

using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class ProgressPacerTests
{
    [Fact]
    public void ShouldYield_IsFalseWhileTheIntervalHasNotElapsed()
    {
        var pacer = new ProgressPacer();

        var yields = 0;
        for (var i = 0; i < 100_000; i++)
        {
            if (pacer.ShouldYield(i))
                yields++;
        }

        yields.ShouldBe(0);
    }

    /// <summary>
    /// The point of pacing by time is that a longer loop yields no more often than a short one, so a report
    /// with ten times the events does not pay ten times the re-render cost.
    /// </summary>
    [Fact]
    public void ShouldYield_TracksElapsedTimeRatherThanIterationCount()
    {
        var pacer = new ProgressPacer();
        var elapsed = Stopwatch.StartNew();

        var yields = 0;
        var iterations = 0;
        while (elapsed.ElapsedMilliseconds < 500)
        {
            if (pacer.ShouldYield(iterations))
                yields++;
            iterations++;
        }

        iterations.ShouldBeGreaterThan(ProgressPacer.CheckInterval);
        yields.ShouldBeInRange(5, 15);
    }

    [Fact]
    public void ShouldYield_OnlyConsidersIterationsOnTheCheckInterval()
    {
        var pacer = new ProgressPacer();
        Thread.Sleep(150);

        pacer.ShouldYield(ProgressPacer.CheckInterval - 1).ShouldBeFalse();
        pacer.ShouldYield(ProgressPacer.CheckInterval).ShouldBeTrue();
    }

    [Fact]
    public void ShouldYield_ArmsTheNextIntervalAfterYielding()
    {
        var pacer = new ProgressPacer();
        Thread.Sleep(150);

        pacer.ShouldYield(0).ShouldBeTrue();
        pacer.ShouldYield(ProgressPacer.CheckInterval).ShouldBeFalse();
    }
}
