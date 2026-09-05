using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using SylvieKit = FellowshipAnalyzer.Heroes.Sylvie.Modules.SylvieKit;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class PinkFlutterflyTests
{
    [Fact]
    public async Task AFlutterflyHealsTheAllyItIsAssignedTo()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 500, overheal: 100),
            Heal(PullStart + 4_000, Spells.FluttercallHealHot, TankId, amount: 400, overheal: 200),
            RemoveBuff(PullStart + 5_000, Spells.FluttercallHealHot, TankId));

        var Flutterfly = parser.PinkFlutterflyTracker.ShouldNotBeNull().Flutterflies.ShouldHaveSingleItem();

        Flutterfly.Unit.ShouldBe(new UnitKey(TankId, 0));
        Flutterfly.Start.ShouldBe(PullStart + 1_000);
        Flutterfly.End.ShouldBe(PullStart + 5_000);
        Flutterfly.Ticks.ShouldBe(2);
        Flutterfly.Effective.ShouldBe(900);
        Flutterfly.Overheal.ShouldBe(300);
    }

    [Fact]
    public async Task MovingAFlutterflyOpensASecondAssignmentRatherThanExtendingTheFirst()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 500),
            RemoveBuff(PullStart + 3_000, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart + 4_000, Spells.FluttercallHealHot, AllyId),
            Heal(PullStart + 5_000, Spells.FluttercallHealHot, AllyId, amount: 700));

        var Flutterflies = parser.PinkFlutterflyTracker.ShouldNotBeNull().Flutterflies.ToList();

        Flutterflies.Count.ShouldBe(2);
        Flutterflies[0].Unit.ShouldBe(new UnitKey(TankId, 0));
        Flutterflies[0].Effective.ShouldBe(500);
        Flutterflies[1].Unit.ShouldBe(new UnitKey(AllyId, 0));
        Flutterflies[1].Effective.ShouldBe(700);
        Flutterflies[1].End.ShouldBeNull();
    }

    [Fact]
    public async Task ATickWithNoAssignmentUnderItIsNotAttributedToAnybody()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.FluttercallHealHot, TankId, amount: 500, overheal: 100));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();

        tracker.Flutterflies.ShouldBeEmpty();
        tracker.UnattributedTicks.ShouldBe(1);
        tracker.UnattributedHealing.ShouldBe(600);
    }

    [Fact]
    public async Task TheUnassignedCountIsReadFromTheTertiaryResource()
    {
        var parser = await Analyze(
            FlutterflySample(PullStart + 1_000, unassigned: 4),
            FlutterflySample(PullStart + 2_000, unassigned: 4),
            FlutterflySample(PullStart + 3_000, unassigned: 1));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();

        tracker.PeakUnassigned.ShouldBe(4);
        tracker.UnassignedAt(PullStart + 2_500).ShouldBe(4);
        tracker.UnassignedAt(PullStart + 3_500).ShouldBe(1);
        tracker.UnassignedSamples.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AssignedTimeIsClippedToTheWindowAsked()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, AllyId),
            RemoveBuff(PullStart + 11_000, Spells.FluttercallHealHot, TankId));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();

        tracker.AssignedMsBetween(PullStart + 1_000, PullStart + 11_000).ShouldBe(20_000);
        tracker.HoldersBetween(PullStart + 1_000, PullStart + 11_000).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ThePullAnalyzerReadsFlutterfliesAssignedBeforeItStarted()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 5_000, Spells.FluttercallHealHot, TankId, amount: 900, overheal: 100));

        var analyzer = parser.PinkFlutterflyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AssignedMs.ShouldBe(PullEnd - PullStart);
        analyzer.AssignableMs.ShouldBe(4L * (PullEnd - PullStart));
        analyzer.AssignedShare.ShouldBe(0.25, 0.001);
        analyzer.Effective.ShouldBe(900);
        analyzer.Overheal.ShouldBe(100);
        analyzer.Holders.ShouldBe(1);
        analyzer.AssignmentsOpened.ShouldBe(0);
    }

    [Fact]
    public async Task AllFourFlutterfliesOutForTheWholePullReadsAsFullyAssigned()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, AllyId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, PlayerId),
            ApplyBuff(PullStart - 500, Spells.FluttercallHealHot, 119));

        var analyzer = parser.PinkFlutterflyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AssignedShare.ShouldBe(1d, 0.001);
    }

    [Fact]
    public async Task RestoreLifeIsTrackedAlongsideHeal()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallRestoreLifeHot, TankId),
            Heal(PullStart + 2_500, Spells.FluttercallRestoreLifeHot, TankId, amount: 300, overheal: 50),
            Heal(PullStart + 4_000, Spells.FluttercallRestoreLifeHot, TankId, amount: 200, overheal: 10),
            RemoveBuff(PullStart + 16_000, Spells.FluttercallRestoreLifeHot, TankId));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();
        var Flutterfly = tracker.Flutterflies.ShouldHaveSingleItem();

        Flutterfly.Unit.ShouldBe(new UnitKey(TankId, 0));
        Flutterfly.Ticks.ShouldBe(2);
        Flutterfly.Effective.ShouldBe(500);
        Flutterfly.Overheal.ShouldBe(60);
        tracker.UnattributedTicks.ShouldBe(0);
    }

    [Fact]
    public async Task AnAssignmentHasTheAbilityThatPlacedIt()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart + 2_000, Spells.FluttercallRestoreLifeHot, AllyId));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();

        tracker.Flutterflies.Count().ShouldBe(2);
        PinkFlutterflyTracker.PlacementOf(tracker.HealAssignments.ShouldHaveSingleItem())
            .ShouldBe(FlutterflyPlacement.Heal);
        PinkFlutterflyTracker.PlacementOf(tracker.RestoreLifeAssignments.ShouldHaveSingleItem())
            .ShouldBe(FlutterflyPlacement.RestoreLife);
    }

    [Fact]
    public async Task ARestoreLifeWindowCountsAsAPairOfFlutterflies()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.FluttercallHealHot, TankId),
            ApplyBuff(PullStart, Spells.FluttercallRestoreLifeHot, AllyId),
            RemoveBuff(PullStart + 20_000, Spells.FluttercallRestoreLifeHot, AllyId));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();

        tracker.AssignedMsBetween(PullStart, PullEnd, FlutterflyPlacement.RestoreLife)
            .ShouldBe(20_000 * SylvieKit.RestoreLifeFlutterflies);
        tracker.AssignedMsBetween(PullStart, PullEnd, FlutterflyPlacement.Heal).ShouldBe(PullEnd - PullStart);
        tracker.AssignedMsBetween(PullStart, PullEnd).ShouldBe(40_000 + (PullEnd - PullStart));
    }

    [Fact]
    public async Task AHolderWithARestoreLifePairIsCreditedForBothFlutterflies()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.FluttercallRestoreLifeHot, AllyId),
            RemoveBuff(PullStart + 20_000, Spells.FluttercallRestoreLifeHot, AllyId));

        var tracker = parser.PinkFlutterflyTracker.ShouldNotBeNull();
        var holder = tracker.HoldersBetween(PullStart, PullEnd).ShouldHaveSingleItem();

        holder.AssignedMs.ShouldBe(40_000);
        holder.Assignments.ShouldBe(1);
        tracker.HoldersBetween(PullStart, PullEnd).Sum(entry => entry.AssignedMs)
            .ShouldBe(tracker.AssignedMsBetween(PullStart, PullEnd));
    }

    [Fact]
    public async Task TwoRestoreLifePairsOutForTheWholePullReadsAsFullyAssigned()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallRestoreLifeHot, TankId),
            ApplyBuff(PullStart - 500, Spells.FluttercallRestoreLifeHot, AllyId));

        var analyzer = parser.PinkFlutterflyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AssignedShare.ShouldBe(1d, 0.001);
    }

    [Fact]
    public async Task ARestoreLifeWindowClosesOnItsRemovalRatherThanAModelledDuration()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallRestoreLifeHot, TankId),
            RefreshBuff(PullStart + 9_000, Spells.FluttercallRestoreLifeHot, TankId),
            RemoveBuff(PullStart + 40_000, Spells.FluttercallRestoreLifeHot, TankId));

        var Flutterfly = parser.PinkFlutterflyTracker.ShouldNotBeNull().RestoreLifeAssignments.ShouldHaveSingleItem();

        Flutterfly.Start.ShouldBe(PullStart + 1_000);
        Flutterfly.End.ShouldBe(PullStart + 40_000);
        Flutterfly.Refreshes.ShouldHaveSingleItem().ShouldBe(PullStart + 9_000);
    }

    [Fact]
    public async Task ThePullAnalyzerReportsRestoreLifeHealingApartFromTheTotal()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 400, overheal: 100),
            ApplyBuff(PullStart + 3_000, Spells.FluttercallRestoreLifeHot, AllyId),
            Heal(PullStart + 4_000, Spells.FluttercallRestoreLifeHot, AllyId, amount: 250, overheal: 25),
            Heal(PullStart + 5_500, Spells.FluttercallRestoreLifeHot, AllyId, amount: 150, overheal: 5),
            RemoveBuff(PullStart + 18_000, Spells.FluttercallRestoreLifeHot, AllyId));

        var analyzer = parser.PinkFlutterflyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Effective.ShouldBe(800);
        analyzer.Overheal.ShouldBe(130);
        analyzer.RestoreLifeTicks.ShouldBe(2);
        analyzer.RestoreLifeEffective.ShouldBe(400);
        analyzer.RestoreLifeOverheal.ShouldBe(30);
        analyzer.RestoreLifeAssignmentsOpened.ShouldBe(1);
        analyzer.RestoreLifeAssignedMs.ShouldBe(30_000);
        analyzer.AssignmentsOpened.ShouldBe(2);
        analyzer.Holders.ShouldBe(2);
        analyzer.AssignedMs.ShouldBe(59_000 + 30_000);
        analyzer.AssignedShare.ShouldBe(89_000 / 240_000d, 0.0001);
    }
}
