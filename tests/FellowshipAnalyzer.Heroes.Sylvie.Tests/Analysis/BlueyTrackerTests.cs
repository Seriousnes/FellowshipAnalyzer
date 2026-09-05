using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class BlueyTrackerTests
{
    [Fact]
    public async Task SendingBlueyToAnAllyOpensAPostingOnThatAlly()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.FluttercallProtect, targetId: TankId),
            ApplyBuff(PullStart + 1_000, Spells.FluttercallProtectBuff, TankId));

        var posting = parser.BlueyTracker.ShouldNotBeNull().Postings.ShouldHaveSingleItem();

        posting.TargetId.ShouldBe(TankId);
        posting.OnSylvie.ShouldBeFalse();
        posting.Start.ShouldBe(PullStart + 1_000);
    }

    [Fact]
    public async Task RecallingBlueyClosesTheAllyPostingAndOpensASelfPosting()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 11_000, Spells.FluttercallEmbraceBuff, PlayerId),
            RemoveBuff(PullStart + 11_000, Spells.FluttercallProtectBuff, TankId));

        var tracker = parser.BlueyTracker.ShouldNotBeNull();

        tracker.Postings.Count.ShouldBe(2);
        tracker.Postings[0].TargetId.ShouldBe(TankId);
        tracker.Postings[0].End.ShouldBe(PullStart + 11_000);
        tracker.Postings[1].OnSylvie.ShouldBeTrue();
        tracker.AllyMsBetween(PullStart, PullEnd).ShouldBe(10_000);
        tracker.SelfMsBetween(PullStart, PullEnd).ShouldBe(PullEnd - (PullStart + 11_000));
    }

    [Fact]
    public async Task ARemovalWithNoApplicationBackdatesThePostingToTheDungeonStart()
    {
        var parser = await Analyze(
            RemoveBuff(PullStart + 4_000, Spells.FluttercallEmbraceBuff, PlayerId));

        var posting = parser.BlueyTracker.ShouldNotBeNull().Postings.ShouldHaveSingleItem();

        posting.OnSylvie.ShouldBeTrue();
        posting.Start.ShouldBe(0);
        posting.End.ShouldBe(PullStart + 4_000);
    }

    [Fact]
    public async Task ThePullAnalyzerSplitsTimeBetweenTheAllyAndSylvie()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallProtectBuff, TankId),
            RemoveBuff(PullStart + 21_000, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 21_000, Spells.FluttercallEmbraceBuff, PlayerId));

        var analyzer = parser.BlueyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.OnAllyMs.ShouldBe(21_000);
        analyzer.OnSylvieMs.ShouldBe(PullEnd - PullStart - 21_000);
        analyzer.UnassignedMs.ShouldBe(0);
        analyzer.Holders.ShouldBe(2);
        analyzer.OnAllyShare.ShouldBe(21_000 / 60_000d, 0.001);
    }

    [Fact]
    public async Task CastsThatMoveBlueyAreCounted()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.FluttercallProtect, targetId: TankId),
            Cast(PullStart + 20_000, Spells.FluttercallEmbrace, targetId: PlayerId));

        parser.BlueyTracker.ShouldNotBeNull().Reassignments.ShouldBe(2);
    }

    [Fact]
    public async Task OnAnAllyBlueyAccountsForHalfOfThatAllysFlutterflyHealing()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 500, Spells.FluttercallHealHot, TankId),
            Heal(PullStart + 3_000, Spells.FluttercallHealHot, TankId, amount: 600, overheal: 200),
            Heal(PullStart + 6_000, Spells.FluttercallRestoreLifeHot, TankId, amount: 400));

        var analyzer = parser.BlueyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.TicksOnHolder.ShouldBe(2);
        analyzer.HealingAddedOnAlly.ShouldBe(500);
        analyzer.HealingAddedOnSylvie.ShouldBe(0);
        analyzer.HealingAdded.ShouldBe(500);
    }

    [Fact]
    public async Task RecalledOntoSylvieBlueyAccountsForAThirdOfHerOwnFlutterflyHealing()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallEmbraceBuff, PlayerId),
            Heal(PullStart + 3_000, Spells.FluttercallHealHot, PlayerId, amount: 900));

        var analyzer = parser.BlueyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.TicksOnHolder.ShouldBe(1);
        analyzer.HealingAddedOnSylvie.ShouldBe(300);
        analyzer.HealingAddedOnAlly.ShouldBe(0);
    }

    [Fact]
    public async Task HealingOnSomebodyOtherThanBlueysHolderAddsNothing()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallProtectBuff, TankId),
            Heal(PullStart + 3_000, Spells.FluttercallHealHot, AllyId, amount: 600));

        var analyzer = parser.BlueyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.TicksOnHolder.ShouldBe(0);
        analyzer.HealingAdded.ShouldBe(0);
    }

    [Fact]
    public async Task ThePullAnalyzerReportsTheManaTheDiscountSaved()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallEmbraceBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.HeartBloom));

        var analyzer = parser.BlueyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.ManaSaved.ShouldBe(118 - (int)Math.Round(118 * SylvieKit.EmbraceManaCostScaler));
    }
}
