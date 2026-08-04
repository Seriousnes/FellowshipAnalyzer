using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class ManaEfficiencyTests
{
    [Fact]
    public async Task CastsArePricedFromTheGameDataBecauseTheLogCarriesNoCost()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Cast(PullStart + 2_000, Spells.Nettlebolt));

        var tracker = parser.SylvieManaTracker.ShouldNotBeNull();

        tracker.Spends.Count.ShouldBe(2);
        tracker.SpentBetween(PullStart, PullEnd).ShouldBe(118 + 5);
    }

    [Fact]
    public async Task BlueyOnSylvieDiscountsEveryCastItCovers()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallEmbraceBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.HeartBloom));

        parser.SylvieManaTracker.ShouldNotBeNull().SpentBetween(PullStart, PullEnd)
            .ShouldBe((int)Math.Round(118 * SylvieKit.EmbraceManaCostScaler));
    }

    [Fact]
    public async Task BlueyOnAnAllyLeavesTheListPriceAlone()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallProtectBuff, TankId),
            Cast(PullStart + 1_000, Spells.HeartBloom));

        parser.SylvieManaTracker.ShouldNotBeNull().SpentBetween(PullStart, PullEnd).ShouldBe(118);
    }

    [Fact]
    public async Task TheDiscountIsAccumulatedAsManaSaved()
    {
        var discounted = (int)Math.Round(118 * SylvieKit.EmbraceManaCostScaler);

        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallEmbraceBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Cast(PullStart + 2_000, Spells.Nettlebolt));

        var tracker = parser.SylvieManaTracker.ShouldNotBeNull();

        tracker.ManaSavedByEmbrace.ShouldBe((118 - discounted) + (5 - (int)Math.Round(5 * SylvieKit.EmbraceManaCostScaler)));
        tracker.SavedBetween(PullStart, PullStart + 1_500).ShouldBe(118 - discounted);
    }

    [Fact]
    public async Task NothingIsSavedWhileBlueySitsOnAnAlly()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 500, Spells.FluttercallProtectBuff, TankId),
            Cast(PullStart + 1_000, Spells.HeartBloom));

        var tracker = parser.SylvieManaTracker.ShouldNotBeNull();

        tracker.ManaSavedByEmbrace.ShouldBe(0);
        tracker.SavedBetween(PullStart, PullEnd).ShouldBe(0);
    }

    [Fact]
    public async Task AnAbilityTheGameDataPricesAtNothingSpendsNothing()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.FluttercallJubilee));

        parser.SylvieManaTracker.ShouldNotBeNull().Spends.ShouldBeEmpty();
    }

    [Fact]
    public async Task TimeAtAFullPoolIsMeasuredByTheGapBetweenReadings()
    {
        var parser = await Analyze(
            ManaSample(PullStart + 1_000, mana: 1_440),
            ManaSample(PullStart + 11_000, mana: 1_440),
            ManaSample(PullStart + 21_000, mana: 700),
            ManaSample(PullStart + 31_000, mana: 700));

        var analyzer = parser.ManaEfficiencyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.MeasuredMs.ShouldBe(30_000);
        analyzer.AtFullMs.ShouldBe(20_000);
        analyzer.AtFullShare.ShouldBe(2 / 3d, 0.001);
        analyzer.LowestShare.ShouldBe(700 / 1_440d, 0.001);
    }

    [Fact]
    public async Task PerSpellSpendingIsGroupedForThePull()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.Nettlebolt),
            Cast(PullStart + 2_000, Spells.Nettlebolt),
            Cast(PullStart + 3_000, Spells.LifePetal));

        var analyzer = parser.ManaEfficiencyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Spent.ShouldBe(5 + 5 + 79);
        analyzer.SpentBySpell[0].SpellId.ShouldBe(Spells.LifePetal.FSLID.Value);
        analyzer.SpentBySpell[0].Mana.ShouldBe(79);
        analyzer.SpentBySpell[1].Casts.ShouldBe(2);
    }

    [Fact]
    public async Task HealingPerManaIsEachSpellsOwnAbsolute()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Heal(PullStart + 3_000, Spells.HeartBloom, TankId, amount: 1_180, overheal: 200),
            Cast(PullStart + 4_000, Spells.LifePetal),
            Heal(PullStart + 5_000, Spells.LifePetal, TankId, amount: 79));

        var tracker = parser.SylvieHealingEfficiencyTracker.ShouldNotBeNull();

        tracker.For(Spells.HeartBloom.FSLID).ShouldNotBeNull().HealingPerResource.ShouldBe(10d, 0.001);
        tracker.For(Spells.LifePetal.FSLID).ShouldNotBeNull().HealingPerResource.ShouldBe(1d, 0.001);
        tracker.TotalResourceSpent.ShouldBe(118 + 79);
    }

    [Fact]
    public async Task ExecutionTimeComesFromTheGlobalCooldownTheCastTriggered()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.HeartBloom),
            Heal(PullStart + 3_000, Spells.HeartBloom, TankId, amount: 1_500));

        var spell = parser.SylvieHealingEfficiencyTracker.ShouldNotBeNull().For(Spells.HeartBloom.FSLID).ShouldNotBeNull();

        spell.ExecutionMs.ShouldBeGreaterThan(0);
        spell.HealingPerExecutionSecond.ShouldBeGreaterThan(0);
    }
}
