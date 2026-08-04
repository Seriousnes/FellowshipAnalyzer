using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;
using SylvieTalents = FellowshipAnalyzer.Core.Common.Spells.SylvieTalents;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class AbsorbWasteAnalyzerTests
{
    [Fact]
    public async Task AShieldThatExpiredWithAbsorbLeftIsMeasuredWaste()
    {
        var parser = await Analyze(
            Combatant(SylvieTalents.NaturalProtector),
            ApplyBuff(PullStart + 1_000, Spells.NaturalProtectorAbsorb, TankId),
            Absorbed(PullStart + 2_000, Spells.NaturalProtectorAbsorb, TankId, amount: 700),
            RemoveBuff(PullStart + 19_000, Spells.NaturalProtectorAbsorb, TankId, absorb: 300));

        var analyzer = parser.AbsorbWasteAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<AbsorbWasteAnalyzer>();

        analyzer.AbsorbsApplied.ShouldBe(1);
        analyzer.AbsorbUsed.ShouldBe(700);
        analyzer.AbsorbWasted.ShouldBe(300);
        analyzer.AbsorbsExpiredUnspent.ShouldBe(1);
        analyzer.AbsorbEfficiency.ShouldBe(0.7, 0.001);
    }

    [Fact]
    public async Task AShieldConsumedToNothingWastesNothing()
    {
        var parser = await Analyze(
            Combatant(SylvieTalents.NaturalProtector),
            ApplyBuff(PullStart + 1_000, Spells.NaturalProtectorAbsorb, TankId),
            Absorbed(PullStart + 2_000, Spells.NaturalProtectorAbsorb, TankId, amount: 1_000),
            RemoveBuff(PullStart + 3_000, Spells.NaturalProtectorAbsorb, TankId, absorb: 0));

        var analyzer = parser.AbsorbWasteAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<AbsorbWasteAnalyzer>();

        analyzer.AbsorbWasted.ShouldBe(0);
        analyzer.AbsorbsExpiredUnspent.ShouldBe(0);
        analyzer.AbsorbEfficiency.ShouldBe(1d, 0.001);
    }

    [Fact]
    public async Task AShieldOnEachAllyIsTrackedSeparately()
    {
        var parser = await Analyze(
            Combatant(SylvieTalents.NaturalProtector),
            ApplyBuff(PullStart + 1_000, Spells.NaturalProtectorAbsorb, TankId),
            ApplyBuff(PullStart + 1_000, Spells.NaturalProtectorAbsorb, AllyId),
            Absorbed(PullStart + 2_000, Spells.NaturalProtectorAbsorb, TankId, amount: 500),
            RemoveBuff(PullStart + 3_000, Spells.NaturalProtectorAbsorb, TankId, absorb: 100),
            RemoveBuff(PullStart + 4_000, Spells.NaturalProtectorAbsorb, AllyId, absorb: 900));

        var analyzer = parser.AbsorbWasteAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<AbsorbWasteAnalyzer>();

        analyzer.AbsorbsApplied.ShouldBe(2);
        analyzer.AbsorbWasted.ShouldBe(1_000);
        analyzer.Absorbs.Count(shield => shield.Target.ActorId == AllyId).ShouldBe(1);
    }

    [Fact]
    public async Task WithoutTheTalentTheAnalyzerIsNeverConstructed()
    {
        var parser = await Analyze(
            Combatant(SylvieTalents.Symbiosis),
            ApplyBuff(PullStart + 1_000, Spells.NaturalProtectorAbsorb, TankId),
            RemoveBuff(PullStart + 3_000, Spells.NaturalProtectorAbsorb, TankId, absorb: 500));

        parser.AbsorbWasteAnalyzers.ShouldBeEmpty();
    }
}

public sealed class CureAilmentAnalyzerTests
{
    [Fact]
    public async Task DispelsAreTalliedPerAbilityAndPerRemovedAura()
    {
        var parser = await Analyze(
            Dispel(PullStart + 1_000, Spells.CureAilment, TankId, Spells.EnfeeblingRootsapDebuff),
            Dispel(PullStart + 8_000, Spells.CureAilment, AllyId, Spells.EnfeeblingRootsapDebuff),
            Dispel(PullStart + 15_000, Spells.CureAilment, TankId, Spells.NaturalSelectionBuff));

        var analyzer = parser.CureAilmentAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CureAilmentDispels.ShouldBe(3);
        analyzer.OtherDispels.ShouldBe(0);
        analyzer.TargetsCleansed.ShouldBe(2);
        analyzer.RemovedAuras[0].Count.ShouldBe(2);
    }

    [Fact]
    public async Task ADispelFromAnotherAbilityIsCountedApart()
    {
        var parser = await Analyze(
            Dispel(PullStart + 1_000, Spells.CureAilment, TankId, Spells.EnfeeblingRootsapDebuff),
            Dispel(PullStart + 2_000, Spells.HiddenTrail, AllyId, Spells.EnfeeblingRootsapDebuff));

        var analyzer = parser.CureAilmentAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CureAilmentDispels.ShouldBe(1);
        analyzer.OtherDispels.ShouldBe(1);
        analyzer.Dispels.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TheFightLifetimeTrackerKeepsEveryDispel()
    {
        var parser = await Analyze(
            Dispel(PullStart + 1_000, Spells.CureAilment, TankId, Spells.EnfeeblingRootsapDebuff),
            Dispel(PullStart + 2_000, Spells.CureAilment, AllyId, Spells.EnfeeblingRootsapDebuff));

        var tracker = parser.DispelTracker.ShouldNotBeNull();

        tracker.TotalDispels.ShouldBe(2);
        tracker.DispelsWith(Spells.CureAilment.FSLID).ShouldBe(2);
        tracker.ByTarget.Count.ShouldBe(2);
    }
}
