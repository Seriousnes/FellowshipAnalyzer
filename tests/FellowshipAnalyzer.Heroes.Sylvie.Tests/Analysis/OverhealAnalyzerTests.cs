using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class OverhealAnalyzerTests
{
    [Fact]
    public async Task EachAbilityIsMeasuredAgainstItsOwnHealing()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.LifePetal, TankId, amount: 100, overheal: 900),
            Heal(PullStart + 2_000, Spells.HeartBloom, TankId, amount: 900, overheal: 100));

        var analyzer = parser.OverhealAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.For(Spells.LifePetal.FSLID).ShouldNotBeNull().OverhealShare.ShouldBe(0.9, 0.001);
        analyzer.For(Spells.HeartBloom.FSLID).ShouldNotBeNull().OverhealShare.ShouldBe(0.1, 0.001);
    }

    [Fact]
    public async Task TicksAccumulateOntoTheAbilityRatherThanTheEffect()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.LifePetal, TankId, amount: 100, overheal: 100),
            Heal(PullStart + 1_500, Spells.LifePetal, AllyId, amount: 300, overheal: 500),
            Heal(PullStart + 2_000, Spells.LifePetal, PlayerId, amount: 100, overheal: 400));

        var lifePetal = parser.OverhealAnalyzers.ShouldHaveSingleItem().Analyzer.For(Spells.LifePetal.FSLID).ShouldNotBeNull();

        lifePetal.Heals.ShouldBe(3);
        lifePetal.Effective.ShouldBe(500);
        lifePetal.Overheal.ShouldBe(1_000);
        lifePetal.TotalHealing.ShouldBe(1_500);
    }

    [Fact]
    public async Task ThePullTotalIsEveryAbilityAddedTogether()
    {
        var parser = await Analyze(
            Heal(PullStart + 1_000, Spells.LifePetal, TankId, amount: 200, overheal: 800),
            Heal(PullStart + 2_000, Spells.FluttercallHealHot, TankId, amount: 300, overheal: 700));

        var analyzer = parser.OverhealAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.TotalEffective.ShouldBe(500);
        analyzer.TotalOverheal.ShouldBe(1_500);
        analyzer.OverhealShare.ShouldBe(0.75, 0.001);
        analyzer.BySpell.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnAbilityThatNeverHealedHasNoRow()
    {
        var parser = await Analyze(Heal(PullStart + 1_000, Spells.LifePetal, TankId, amount: 100));

        parser.OverhealAnalyzers.ShouldHaveSingleItem().Analyzer.For(Spells.HeartBloom.FSLID).ShouldBeNull();
    }
}
