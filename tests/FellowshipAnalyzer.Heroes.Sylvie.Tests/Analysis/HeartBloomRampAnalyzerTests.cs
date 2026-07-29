using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class HeartBloomRampAnalyzerTests
{
    [Fact]
    public async Task ACastBanksTheFlutterflyHealingThatCameBeforeIt()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.FluttercallHealHot, TankId, amount: 400, overheal: 600),
            Heal(PullStart + 3_000, Spells.FluttercallHealHot, AllyId, amount: 200, overheal: 800),
            Cast(PullStart + 5_000, Spells.HeartBloom),
            Heal(PullStart + 7_000, Spells.HeartBloom, TankId, amount: 900, overheal: 100));

        var cast = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts.ShouldHaveSingleItem();

        cast.FlutterflyEffective.ShouldBe(600);
        cast.FlutterflyOverheal.ShouldBe(1_400);
        cast.FlutterflyFed.ShouldBe(2_000);
        cast.Effective.ShouldBe(900);
        cast.Overheal.ShouldBe(100);
        cast.AccumulationMs.ShouldBe(5_000);
    }

    [Fact]
    public async Task TheAccumulationWindowResetsAtEachCast()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.FluttercallHealHot, TankId, amount: 1_000),
            Cast(PullStart + 2_000, Spells.HeartBloom),
            Heal(PullStart + 3_000, Spells.FluttercallHealHot, TankId, amount: 500),
            Cast(PullStart + 4_000, Spells.HeartBloom));

        var casts = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts;

        casts.Count.ShouldBe(2);
        casts[0].FlutterflyFed.ShouldBe(1_000);
        casts[1].FlutterflyFed.ShouldBe(500);
        casts[1].AccumulationMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task ReleasesAreCountedByTheirTwoSecondInterval()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Heal(PullStart + 3_000, Spells.HeartBloom, TankId, amount: 100),
            Heal(PullStart + 3_001, Spells.HeartBloom, AllyId, amount: 100),
            Heal(PullStart + 5_000, Spells.HeartBloom, TankId, amount: 100),
            Heal(PullStart + 7_000, Spells.HeartBloom, TankId, amount: 100));

        var cast = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts.ShouldHaveSingleItem();

        cast.Releases.ShouldBe(3);
        cast.ReleasedInFull.ShouldBeFalse();
        SylvieKit.HeartBloomReleases.ShouldBe(8);
    }

    [Fact]
    public async Task WhereBlueyWasIsRecordedOnTheCast()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallProtectBuff, TankId),
            Cast(PullStart + 1_000, Spells.HeartBloom),
            RemoveBuff(PullStart + 2_000, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 2_000, Spells.FluttercallEmbraceBuff, PlayerId),
            Cast(PullStart + 3_000, Spells.HeartBloom));

        var casts = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts;

        casts[0].BlueyOnAlly.ShouldBeTrue();
        casts[0].BlueyAssigned.ShouldBeTrue();
        casts[1].BlueyOnAlly.ShouldBeFalse();
        casts[1].BlueyAssigned.ShouldBeTrue();
    }

    [Fact]
    public async Task ACastWithBlueyNowhereReadsAsUnplaced()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.HeartBloom));

        var analyzer = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastsWithoutBluey.ShouldBe(1);
        analyzer.CastsWithBlueyOnAlly.ShouldBe(0);
    }
}
