using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class LifePetalAnalyzerTests
{
    [Fact]
    public async Task ANettleboltAtFullChargesGeneratesReductionThatShortensNothing()
    {
        var parser = await Analyze(
            Damage(PullStart + 1_000, Spells.Nettlebolt, amount: 500));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltHits.ShouldBe(1);
        analyzer.NettleboltsAtFullCharges.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(SylvieKit.NettleboltCooldownReductionMs);
        analyzer.CooldownReduction.Effective.ShouldBe(0);
        analyzer.CooldownReduction.Wasted.ShouldBe(SylvieKit.NettleboltCooldownReductionMs);
        analyzer.CooldownReduction.Efficiency.ShouldBe(0d);
    }

    [Fact]
    public async Task ACritGeneratesTwiceTheReduction()
    {
        var parser = await Analyze(
            Damage(PullStart + 1_000, Spells.Nettlebolt, amount: 900, hitType: HitType.Crit));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltCrits.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(SylvieKit.NettleboltCritCooldownReductionMs);
    }

    [Fact]
    public async Task ReductionAfterACastShortensTheRunningRecharge()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.LifePetal),
            Cast(PullStart + 2_000, Spells.LifePetal),
            Damage(PullStart + 3_000, Spells.Nettlebolt, amount: 500),
            Damage(PullStart + 4_000, Spells.Nettlebolt, amount: 500, hitType: HitType.Crit));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBe(2);
        analyzer.NettleboltsAtFullCharges.ShouldBe(0);
        analyzer.CooldownReduction.Effective.ShouldBe(analyzer.CooldownReduction.Total);
        analyzer.CooldownReduction.Efficiency.ShouldBe(1d, 0.001);
    }

    [Theory]
    [InlineData(HitType.Normal, 1_500)]
    [InlineData(HitType.Crit, 3_000)]
    public async Task ANettleboltHitTakesItsReductionOffTheRunningRecharge(HitType hitType, int reductionMs)
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.LifePetal),
            Cast(PullStart + 2_000, Spells.LifePetal),
            Damage(PullStart + 3_000, Spells.Nettlebolt, amount: 500, hitType: hitType));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CooldownReduction.Total.ShouldBe(reductionMs);
        analyzer.CooldownReduction.Effective.ShouldBe(reductionMs);
    }

    [Fact]
    public async Task LifePetalRechargesInSixtySecondsWithEmeraldAtRankTenAndHasteZero()
    {
        var combatant = Combatant();
        combatant.Emerald = 1500;

        var parser = await Analyze(
            combatant,
            Cast(PullStart + 1_000, Spells.LifePetal));

        parser.GetModule<SpellUsable>().ShouldNotBeNull()
            .RechargeDuration(Spells.LifePetal.FSLID).ShouldBe(60_000);
    }

    [Fact]
    public async Task TimeHeldAtBothChargesIsMeasuredAgainstTheTimeSampled()
    {
        var parser = await Analyze(
            Damage(PullStart + 1_000, Spells.Nettlebolt, amount: 500),
            Cast(PullStart + 11_000, Spells.LifePetal),
            Damage(PullStart + 21_000, Spells.Nettlebolt, amount: 500));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.MeasuredMs.ShouldBeGreaterThan(0);
        analyzer.ChargeCappedMs.ShouldBe(10_000);
        analyzer.ChargeCappedShare.ShouldBeGreaterThan(0);
        analyzer.ChargeCappedShare.ShouldBeLessThan(1);
    }

    [Fact]
    public async Task ANettleboltThatNeverHitGeneratesNothing()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Nettlebolt));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltHits.ShouldBe(0);
        analyzer.CooldownReduction.Total.ShouldBe(0);
    }
}
