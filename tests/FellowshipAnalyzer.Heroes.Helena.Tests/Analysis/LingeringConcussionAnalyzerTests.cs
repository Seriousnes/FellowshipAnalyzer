using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class LingeringConcussionAnalyzerTests
{
    private static readonly Core.Common.Spells.Effect Concussion =
        Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff;

    [Fact]
    public async Task Uptime_IsTheCoveredShareOfThePull()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            RemoveDebuff(PullStart + 30_000, Concussion));

        analyzer.Uptime.ShouldBe(0.5, 0.001);
        analyzer.CoveredMs.ShouldBe(30_000);
    }

    [Fact]
    public async Task StackTime_IsSplitByTheStackCountHeldOverEachStretch()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            ApplyDebuffStack(PullStart + 10_000, Concussion, 5),
            RemoveDebuff(PullStart + 30_000, Concussion));

        analyzer.MsAtStacks[1].ShouldBe(10_000);
        analyzer.MsAtStacks[5].ShouldBe(20_000);
        analyzer.AtCapMs.ShouldBe(20_000);
        analyzer.BelowCapMs.ShouldBe(10_000);
        analyzer.PeakStacks.ShouldBe(5);
    }

    [Fact]
    public async Task AtCapShare_IsMeasuredAgainstCoveredTimeAndAtCapUptimeAgainstThePull()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            ApplyDebuffStack(PullStart + 10_000, Concussion, 5),
            RemoveDebuff(PullStart + 30_000, Concussion));

        analyzer.AtCapShare.ShouldBe(20_000 / 30_000d, 0.001);
        analyzer.AtCapUptime.ShouldBe(20_000 / 60_000d, 0.001);
    }

    [Fact]
    public async Task AverageReduction_WeightsThreePercentPerStackByTimeHeld()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            ApplyDebuffStack(PullStart + 30_000, Concussion, 5),
            RemoveDebuff(PullEnd, Concussion));

        analyzer.AverageReduction.ShouldBe((0.03 * 0.5) + (0.15 * 0.5), 0.001);
    }

    [Fact]
    public async Task StacksAreScopedToThePrimaryTargetRatherThanEveryEnemy()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion, targetInstance: 1),
            ApplyDebuffStack(PullStart + 5_000, Concussion, 5, targetInstance: 1),
            RemoveDebuff(PullStart + 40_000, Concussion, targetInstance: 1),
            ApplyDebuff(PullStart + 1_000, Concussion, targetInstance: 2),
            RemoveDebuff(PullStart + 3_000, Concussion, targetInstance: 2));

        analyzer.PrimaryTarget.ShouldNotBeNull().TargetInstance.ShouldBe(1);
        analyzer.CoveredMs.ShouldBe(40_000);
        analyzer.AtCapMs.ShouldBe(35_000);
    }

    [Fact]
    public async Task GapsCountTheStretchesBetweenWindowsOnThePrimaryTarget()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            RemoveDebuff(PullStart + 10_000, Concussion),
            ApplyDebuff(PullStart + 15_000, Concussion),
            RemoveDebuff(PullStart + 40_000, Concussion));

        analyzer.GapCount.ShouldBe(1);
        analyzer.TotalGapMs.ShouldBe(5_000);
        analyzer.CoveredMs.ShouldBe(35_000);
    }

    [Fact]
    public async Task ADebuffStillUpAtThePullEnd_IsMeasuredToItsLastObservation()
    {
        var analyzer = await Analyze(
            ApplyDebuff(PullStart, Concussion),
            ApplyDebuffStack(PullStart + 20_000, Concussion, 3));

        analyzer.PeakStacks.ShouldBe(3);
        analyzer.CoveredMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task APullTheDebuffWasNeverAppliedIn_ReadsZeroRatherThanThrowing()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.ShieldSlam));

        analyzer.PrimaryTarget.ShouldBeNull();
        analyzer.Uptime.ShouldBe(0);
        analyzer.CoveredMs.ShouldBe(0);
        analyzer.AtCapMs.ShouldBe(0);
        analyzer.AverageReduction.ShouldBe(0);
    }

    [Fact]
    public async Task TheCapAndPerStackReduction_MatchTheSeasonThreeConstants()
    {
        LingeringConcussionAnalyzer.MaxStacks.ShouldBe(5);
        LingeringConcussionAnalyzer.ReductionPerStack.ShouldBe(0.03);
    }

    private static async Task<LingeringConcussionAnalyzer> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return (LingeringConcussionAnalyzer)parser.LingeringConcussionAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
