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
    public async Task TwoBloomsAliveAtOnceEachKeepTheirOwnReleases()
    {
        var parser = await Analyze(
            Combatant(Core.Common.Spells.SylvieTalents.FlowerPower),
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Heal(PullStart + 3_000, Spells.HeartBloom, TankId, amount: 100),
            Heal(PullStart + 5_000, Spells.HeartBloom, TankId, amount: 100),
            Cast(PullStart + 6_000, Spells.HeartBloom),
            Heal(PullStart + 7_000, Spells.HeartBloom, TankId, amount: 100),
            Heal(PullStart + 8_000, Spells.HeartBloom, AllyId, amount: 50),
            Heal(PullStart + 9_000, Spells.HeartBloom, TankId, amount: 100),
            Heal(PullStart + 10_000, Spells.HeartBloom, AllyId, amount: 50),
            Heal(PullStart + 22_000, Spells.HeartBloom, AllyId, amount: 50));

        var casts = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts;

        casts.Count.ShouldBe(2);
        casts[0].Releases.ShouldBe(4);
        casts[0].Effective.ShouldBe(400);
        casts[1].Releases.ShouldBe(3);
        casts[1].Effective.ShouldBe(150);
    }

    [Fact]
    public async Task HastedPulsesGoToTheBloomTheirIntervalMatches()
    {
        const int hasteRating = 1_000;
        var haste = Core.Analysis.StatTracker.RatingToPercentage(hasteRating);
        var period = (int)(SylvieKit.HeartBloomTickIntervalMs * 100.0 / (100.0 + haste * 100.0));
        var first = PullStart + 1_000;
        var second = first + (period * 5 / 2);

        var combatant = Combatant(Core.Common.Spells.SylvieTalents.FlowerPower);
        combatant.Haste = hasteRating;

        var parser = await Analyze(
            combatant,
            Cast(first, Spells.HeartBloom),
            Heal(first + period, Spells.HeartBloom, TankId, amount: 100),
            Heal(first + (2 * period), Spells.HeartBloom, TankId, amount: 100),
            Cast(second, Spells.HeartBloom),
            Heal(first + (3 * period), Spells.HeartBloom, TankId, amount: 100),
            Heal(second + period, Spells.HeartBloom, AllyId, amount: 10),
            Heal(first + (4 * period), Spells.HeartBloom, TankId, amount: 100),
            Heal(second + (2 * period), Spells.HeartBloom, AllyId, amount: 10));

        haste.ShouldBeGreaterThan(0.1);
        var casts = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts;

        casts[0].Effective.ShouldBe(400);
        casts[1].Effective.ShouldBe(20);
    }

    [Fact]
    public async Task AHasteChangeDuringTheOverlapRetimesEachBloomsNextPulse()
    {
        const int first = PullStart + 1_000;
        const int second = PullStart + 1_500;
        const int hasteGained = PullStart + 4_000;
        var hastedPeriod = SylvieKit.HeartBloomTickIntervalMs / 1.3;
        var firstRetimed = hasteGained + (0.5 * hastedPeriod);
        var secondRetimed = hasteGained + (0.75 * hastedPeriod);

        var parser = await Analyze(
            Combatant(Core.Common.Spells.SylvieTalents.FlowerPower),
            Cast(first, Spells.HeartBloom),
            Cast(second, Spells.HeartBloom),
            Heal(first + SylvieKit.HeartBloomTickIntervalMs, Spells.HeartBloom, TankId, amount: 100),
            Heal(second + SylvieKit.HeartBloomTickIntervalMs, Spells.HeartBloom, AllyId, amount: 10),
            ApplyBuff(hasteGained, Core.Common.Spells.Spells.SpiritOfHeroism, PlayerId),
            Heal((int)Math.Round(firstRetimed), Spells.HeartBloom, TankId, amount: 100),
            Heal((int)Math.Round(secondRetimed), Spells.HeartBloom, AllyId, amount: 10),
            Heal((int)Math.Round(firstRetimed + hastedPeriod), Spells.HeartBloom, TankId, amount: 100),
            Heal((int)Math.Round(secondRetimed + hastedPeriod), Spells.HeartBloom, AllyId, amount: 10));

        var casts = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer.Casts;

        casts[0].Effective.ShouldBe(300);
        casts[1].Effective.ShouldBe(30);
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
    public async Task ACastWithBlueyNowhereReadsAsUnassigned()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.HeartBloom));

        var analyzer = parser.HeartBloomRampAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastsWithoutBluey.ShouldBe(1);
        analyzer.CastsWithBlueyOnAlly.ShouldBe(0);
    }
}
