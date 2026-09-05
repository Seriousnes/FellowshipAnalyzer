using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class DefensiveAnalyzerTests
{
    [Fact]
    public async Task OnlyHitsInsideAWindow_AreAttributedToTheDefensive()
    {
        var analyzer = await ShieldsUp(
            DamageTaken(PullStart + 500, 100, 1_000, 900),
            ApplyBuff(PullStart + 1_000, Spells.ShieldsUpBuff),
            DamageTaken(PullStart + 2_000, 200, 5_000, 4_500, absorbed: 300),
            RemoveBuff(PullStart + 13_000, Spells.ShieldsUpBuff),
            DamageTaken(PullStart + 14_000, 100, 1_000, 900));

        analyzer.HitsInWindows.ShouldBe(1);
        analyzer.DamageTakenInWindow.ShouldBe(200);
        analyzer.DamageFacedInWindow.ShouldBe(5_000);
        analyzer.MitigatedInWindow.ShouldBe(4_500);
        analyzer.AbsorbedInWindow.ShouldBe(300);
    }

    [Fact]
    public async Task MitigationInWindow_IsMitigatedPlusAbsorbedAndNeverAddsBlocked()
    {
        var analyzer = await ShieldsUp(
            ApplyBuff(PullStart + 1_000, Spells.ShieldsUpBuff),
            DamageTaken(PullStart + 2_000, 0, 10_000, 9_000, absorbed: 1_000, blocked: 4_000, hitType: HitType.Block),
            RemoveBuff(PullStart + 13_000, Spells.ShieldsUpBuff));

        analyzer.MitigationInWindow.ShouldBe(10_000);
        analyzer.BlockedInWindow.ShouldBe(4_000);
        analyzer.BlockedHits.ShouldBe(1);
        analyzer.MitigationShareInWindow.ShouldBe(1d);
    }

    [Fact]
    public async Task ARefreshInsideAnOpenWindow_ExtendsItRatherThanSplittingIt()
    {
        var analyzer = await ShieldsUp(
            ApplyBuff(PullStart + 1_000, Spells.ShieldsUpBuff),
            new RefreshBuffEvent
            {
                Timestamp = PullStart + 5_000,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = Spells.ShieldsUpBuff.FSLID },
                AbilityGameId = Spells.ShieldsUpBuff.FSLID,
            },
            RemoveBuff(PullStart + 13_000, Spells.ShieldsUpBuff));

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ActiveMs.ShouldBe(12_000);
    }

    [Fact]
    public async Task AWindowStillOpenAtThePullEnd_ClosesAtThePullBoundary()
    {
        var analyzer = await ShieldsUp(ApplyBuff(PullEnd - 5_000, Spells.ShieldsUpBuff));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Truncated.ShouldBeTrue();
        window.End.ShouldBe(PullEnd);
        analyzer.ActiveMs.ShouldBe(5_000);
    }

    [Fact]
    public async Task Uptime_IsTheActiveShareOfThePull()
    {
        var analyzer = await ShieldsUp(
            ApplyBuff(PullStart, Spells.ShieldsUpBuff),
            RemoveBuff(PullStart + 30_000, Spells.ShieldsUpBuff));

        analyzer.Uptime.ShouldBe(0.5, 0.001);
    }

    [Fact]
    public async Task ShieldsUp_IsFlaggedDefensiveInTheSpellbook()
    {
        var analyzer = await ShieldsUp(ApplyBuff(PullStart, Spells.ShieldsUpBuff));

        analyzer.TracksDefensive.ShouldBeTrue();
        analyzer.Ability.ShouldNotBeNull().IsDefensive.ShouldBeTrue();
    }

    [Fact]
    public async Task IronWall_IsFlaggedDefensiveSoItsWindowsAreTracked()
    {
        var analyzer = await IronWall(
            ApplyBuff(PullStart + 1_000, Spells.IronWallBuff),
            DamageTaken(PullStart + 2_000, 100, 5_000, 4_900),
            RemoveBuff(PullStart + 13_000, Spells.IronWallBuff));

        analyzer.TracksDefensive.ShouldBeTrue();
        analyzer.WindowCount.ShouldBe(1);
        analyzer.MitigationInWindow.ShouldBe(4_900);
    }

    [Fact]
    public async Task IronWall_RecordsTheToughnessAtTheUse()
    {
        var analyzer = await IronWall(
            Cast(PullStart + 1_000, Spells.IronWall, toughness: 600),
            ApplyBuff(PullStart + 1_000, Spells.IronWallBuff),
            RemoveBuff(PullStart + 13_000, Spells.IronWallBuff));

        var use = analyzer.Uses.ShouldHaveSingleItem();
        use.ToughnessShare.ShouldNotBeNull().ShouldBe(0.6, 0.001);
        use.Band.ShouldBe(ToughnessBand.Level3);
        use.OpenedAboveHalfToughness.ShouldBeTrue();
        analyzer.UsesFromHighToughness.ShouldBe(1);
    }

    [Fact]
    public async Task IronWall_UsedAtLowToughness_IsNotCountedAsAHighToughnessUse()
    {
        var analyzer = await IronWall(
            Cast(PullStart + 1_000, Spells.IronWall, toughness: 100),
            ApplyBuff(PullStart + 1_000, Spells.IronWallBuff),
            RemoveBuff(PullStart + 13_000, Spells.IronWallBuff));

        var use = analyzer.Uses.ShouldHaveSingleItem();
        use.OpenedAboveHalfToughness.ShouldBeFalse();
        analyzer.UsesFromHighToughness.ShouldBe(0);
    }

    [Fact]
    public async Task IronWall_RecordsTheShieldsUpChargesLeftWhenItWasUsed()
    {
        var analyzer = await IronWall(
            Cast(PullStart + 1_000, Spells.ShieldsUp),
            Cast(PullStart + 2_000, Spells.ShieldsUp),
            Cast(PullStart + 3_000, Spells.IronWall, toughness: 900),
            ApplyBuff(PullStart + 3_000, Spells.IronWallBuff),
            RemoveBuff(PullStart + 15_000, Spells.IronWallBuff));

        analyzer.Uses.ShouldHaveSingleItem().ShieldsUpCharges.ShouldBe(0);
        analyzer.UsesWithShieldsUpSpent.ShouldBe(1);
    }

    [Fact]
    public async Task ShieldsUp_CountsTimeHoldingEveryCharge()
    {
        var analyzer = await ShieldsUp(
            Cast(PullStart + 10_000, Spells.ShieldsUp),
            Cast(PullStart + 11_000, Spells.ShieldsUp));

        analyzer.MaxCharges.ShouldBe(2);
        analyzer.Casts.ShouldBe(2);
        analyzer.ChargeCappedMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task ShieldsUp_HeldAtFullChargesAllPull_ReadsTheWholePullAsCapped()
    {
        var analyzer = await ShieldsUp(DamageTaken(PullStart + 1_000, 100, 1_000, 900));

        analyzer.ChargeCappedMs.ShouldBe(PullEnd - PullStart);
        analyzer.ChargeCappedShare.ShouldBe(1d);
    }

    private static async Task<ShieldsUpAnalyzer> ShieldsUp(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return (ShieldsUpAnalyzer)parser.ShieldsUpAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<IronWallAnalyzer> IronWall(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return (IronWallAnalyzer)parser.IronWallAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
