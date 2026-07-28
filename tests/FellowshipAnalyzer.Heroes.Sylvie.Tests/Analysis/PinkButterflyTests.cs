using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class PinkButterflyTests
{
    [Fact]
    public async Task AButterflyHealsTheAllyItIsParkedOn()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 500, overheal: 100),
            Heal(PullStart + 4_000, Spells.FluttercallHealHot, TankId, amount: 400, overheal: 200),
            RemoveBuff(PullStart + 5_000, Spells.FluttercallHealHot, TankId));

        var butterfly = parser.PinkButterflyTracker.ShouldNotBeNull().Butterflies.ShouldHaveSingleItem();

        butterfly.Unit.ShouldBe(new UnitKey(TankId, 0));
        butterfly.Start.ShouldBe(PullStart + 1_000);
        butterfly.End.ShouldBe(PullStart + 5_000);
        butterfly.Ticks.ShouldBe(2);
        butterfly.Effective.ShouldBe(900);
        butterfly.Overheal.ShouldBe(300);
    }

    [Fact]
    public async Task MovingAButterflyOpensASecondAssignmentRatherThanExtendingTheFirst()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 500),
            RemoveBuff(PullStart + 3_000, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart + 4_000, Spells.FluttercallHealHot, AllyId),
            Heal(PullStart + 5_000, Spells.FluttercallHealHot, AllyId, amount: 700));

        var butterflies = parser.PinkButterflyTracker.ShouldNotBeNull().Butterflies.ToList();

        butterflies.Count.ShouldBe(2);
        butterflies[0].Unit.ShouldBe(new UnitKey(TankId, 0));
        butterflies[0].Effective.ShouldBe(500);
        butterflies[1].Unit.ShouldBe(new UnitKey(AllyId, 0));
        butterflies[1].Effective.ShouldBe(700);
        butterflies[1].End.ShouldBeNull();
    }

    [Fact]
    public async Task ATickWithNoAssignmentUnderItIsNotAttributedToAnybody()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.FluttercallHealHot, TankId, amount: 500, overheal: 100));

        var tracker = parser.PinkButterflyTracker.ShouldNotBeNull();

        tracker.Butterflies.ShouldBeEmpty();
        tracker.UnattributedTicks.ShouldBe(1);
        tracker.UnattributedHealing.ShouldBe(600);
    }

    [Fact]
    public async Task TheBankIsReadFromTheTertiaryResource()
    {
        var parser = await Analyze(
            ButterflySample(PullStart + 1_000, banked: 4),
            ButterflySample(PullStart + 2_000, banked: 4),
            ButterflySample(PullStart + 3_000, banked: 1));

        var tracker = parser.PinkButterflyTracker.ShouldNotBeNull();

        tracker.PeakBanked.ShouldBe(4);
        tracker.BankedAt(PullStart + 2_500).ShouldBe(4);
        tracker.BankedAt(PullStart + 3_500).ShouldBe(1);
        tracker.BankSamples.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AssignedTimeIsClippedToTheWindowAsked()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, AllyId),
            RemoveBuff(PullStart + 11_000, Spells.FluttercallHealHot, TankId));

        var tracker = parser.PinkButterflyTracker.ShouldNotBeNull();

        tracker.AssignedMsBetween(PullStart + 1_000, PullStart + 11_000).ShouldBe(20_000);
        tracker.HoldersBetween(PullStart + 1_000, PullStart + 11_000).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ThePullAnalyzerReadsButterfliesParkedBeforeItStarted()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 5_000, Spells.FluttercallHealHot, TankId, amount: 900, overheal: 100));

        var analyzer = parser.PinkButterflyAssignmentAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AssignedMs.ShouldBe(PullEnd - PullStart);
        analyzer.AssignableMs.ShouldBe(4L * (PullEnd - PullStart));
        analyzer.AssignedShare.ShouldBe(0.25, 0.001);
        analyzer.Effective.ShouldBe(900);
        analyzer.Overheal.ShouldBe(100);
        analyzer.HoldersCovered.ShouldBe(1);
        analyzer.AssignmentsOpened.ShouldBe(0);
    }

    [Fact]
    public async Task AllFourButterfliesOutForTheWholePullReadsAsFullCoverage()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, AllyId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, PlayerId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, 119));

        var analyzer = parser.PinkButterflyAssignmentAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AssignedShare.ShouldBe(1d, 0.001);
    }
}
