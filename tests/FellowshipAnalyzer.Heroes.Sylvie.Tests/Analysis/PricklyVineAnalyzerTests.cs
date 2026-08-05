using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class FlutterflyHealingBuffAnalyzerTests
{
    [Fact]
    public async Task OneVineIsAliveForItsModelledLifetime()
    {
        var parser = await Analyze(Cast(PullStart, Spells.PricklyVine));

        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.VineCasts.ShouldBe(1);
        analyzer.PeakLiveVines.ShouldBe(1);
        analyzer.NoVineMs.ShouldBe(analyzer.PullDurationMs - SylvieKit.PricklyVineDurationMs);
        analyzer.AverageLiveVines.ShouldBe(SylvieKit.PricklyVineDurationMs / (double)analyzer.PullDurationMs, 0.01);
    }

    [Fact]
    public async Task ConcurrentVinesStack()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.PricklyVine),
            Cast(PullStart + 2_000, Spells.PricklyVine),
            Cast(PullStart + 4_000, Spells.PricklyVine));

        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.PeakLiveVines.ShouldBe(3);
        analyzer.AverageVineMultiplier.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task TheVineMultiplierIsThreePercentPerLiveVine()
    {
        SylvieKit.FlutterflyHealPerVine.ShouldBe(0.03, 0.0001);

        var parser = await Analyze(Cast(PullStart, Spells.PricklyVine));
        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AverageVineMultiplier
            .ShouldBe(1 + (analyzer.AverageLiveVines * SylvieKit.FlutterflyHealPerVine), 0.0001);
    }

    [Fact]
    public async Task NoVinesAtAllReadsAsAWholePullWithoutOne()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Nettlebolt));

        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.VineCasts.ShouldBe(0);
        analyzer.NoVineShare.ShouldBe(1d, 0.001);
        analyzer.AverageVineMultiplier.ShouldBe(1d);
    }

    [Fact]
    public async Task SafeHavenCoverageCountsWallClockOnceAndAllyTimeSeparately()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.SafeHaven),
            ApplyBuff(PullStart + 1_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId),
            ApplyBuff(PullStart + 1_000, Spells.SafeHavenIncreasedHealingTakenBuff, AllyId),
            RemoveBuff(PullStart + 11_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId),
            RemoveBuff(PullStart + 11_000, Spells.SafeHavenIncreasedHealingTakenBuff, AllyId));

        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.SafeHavenCasts.ShouldBe(1);
        analyzer.SafeHavenCoveredMs.ShouldBe(10_000);
        analyzer.SafeHavenAllyMs.ShouldBe(20_000);
        analyzer.SafeHavenAllies.ShouldBe(2);
        analyzer.SafeHavenUptime.ShouldBe(10_000 / 60_000d, 0.001);
    }

    [Fact]
    public async Task ASafeHavenWindowStillOpenAtPullEndRunsToTheBoundary()
    {
        var parser = await Analyze(
            ApplyBuff(PullEnd - 5_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId));

        var analyzer = parser.FlutterflyHealingBuffAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.SafeHavenCoveredMs.ShouldBe(5_000);
        analyzer.SafeHavenAllies.ShouldBe(1);
    }
}
