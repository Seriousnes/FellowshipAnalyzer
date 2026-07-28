using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class LifePetalAnalyzerTests
{
    [Fact]
    public async Task ANettleboltAtFullChargesGeneratesReductionThatBuysNothing()
    {
        var parser = await Analyze(
            Damage(PullStart + 1_000, Spells.Nettlebolt, amount: 500));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltHits.ShouldBe(1);
        analyzer.NettleboltsAtFullCharges.ShouldBe(1);
        analyzer.ReductionGeneratedMs.ShouldBe(SylvieKit.NettleboltCooldownReductionMs);
        analyzer.ReductionAppliedMs.ShouldBe(0);
        analyzer.ReductionWastedMs.ShouldBe(SylvieKit.NettleboltCooldownReductionMs);
        analyzer.ReductionEfficiency.ShouldBe(0d);
    }

    [Fact]
    public async Task ACritGeneratesTwiceTheReduction()
    {
        var parser = await Analyze(
            Damage(PullStart + 1_000, Spells.Nettlebolt, amount: 900, hitType: HitType.Crit));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltCrits.ShouldBe(1);
        analyzer.ReductionGeneratedMs.ShouldBe(SylvieKit.NettleboltCritCooldownReductionMs);
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
        analyzer.ReductionAppliedMs.ShouldBe(analyzer.ReductionGeneratedMs);
        analyzer.ReductionEfficiency.ShouldBe(1d, 0.001);
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
    public async Task ANettleboltThatNeverLandedGeneratesNothing()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Nettlebolt));

        var analyzer = parser.LifePetalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.NettleboltHits.ShouldBe(0);
        analyzer.ReductionGeneratedMs.ShouldBe(0);
    }
}
