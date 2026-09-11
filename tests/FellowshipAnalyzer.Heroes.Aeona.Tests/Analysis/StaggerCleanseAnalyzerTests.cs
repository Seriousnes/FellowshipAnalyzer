using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class StaggerCleanseAnalyzerTests
{
    private static readonly int[] Echoes = [AeonaTalents.EchoesOfDivinity];

    [Fact]
    public async Task ACast_HasEachAllysStaggerBeforeAndTheStaggerItRemoved()
    {
        var analyzer = await Analyze(Info([]),
            TankStagger(900, staggerHitPoints: 10_000),
            Activation(1_000, Spells.AmendFate, TankId),
            Heal(1_000, Spells.AmendFate, TankId, 5_000, 1_000),
            TankStagger(1_050, staggerHitPoints: 4_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        var heal = cast.Heals.ShouldHaveSingleItem();
        heal.IsTank.ShouldBeTrue();
        heal.EffectiveHealing.ShouldBe(5_000);
        heal.Overheal.ShouldBe(1_000);
        heal.StaggerBefore.ShouldBe(10_000);
        heal.StaggerCleansed.ShouldBe(6_000);
        cast.StaggerRemoved.ShouldBe(6_000);
        cast.BelowStaggerRemoved.ShouldBe(false);
        cast.TankStaggerFraction.ShouldBe(0.25);
        analyzer.StaggerCleansedBy(Spells.AmendFate.FSLID).ShouldBe(6_000);
        analyzer.BracketedCastsOf(Spells.AmendFate.FSLID).ShouldBe(1);
    }

    [Fact]
    public async Task ALowStaggerCastUnder40PercentThatHealsLessThanAnOblivion_IsFlagged()
    {
        var analyzer = await Analyze(Info([]),
            Activation(500, Spells.Oblivion),
            Heal(501, Spells.Oblivion, TankId, 3_000),
            TankStagger(900, staggerHitPoints: 10_000),
            Activation(1_000, Spells.AmendFate, TankId),
            Heal(1_000, Spells.AmendFate, TankId, 5_000),
            TankStagger(1_050, staggerHitPoints: 4_000),
            TankStagger(9_900, staggerHitPoints: 2_000),
            Activation(10_000, Spells.AmendFate, TankId),
            Heal(10_000, Spells.AmendFate, TankId, 1_000),
            TankStagger(10_050, staggerHitPoints: 0));

        analyzer.OblivionValuePerCast.ShouldNotBeNull().ShouldBe(3_000.0, 0.0001);
        var second = analyzer.Casts[1];
        second.BelowStaggerRemoved.ShouldBe(true);
        second.BelowOblivionValue.ShouldBe(true);
        second.Rated.ShouldBeTrue();
        second.Flagged.ShouldBeTrue();
        analyzer.Casts[0].Flagged.ShouldBeFalse();
        analyzer.CastsRated.ShouldBe(2);
        analyzer.FlaggedCasts.ShouldBe(1);
        analyzer.LowStaggerCasts.ShouldBe(1);
        analyzer.BelowOblivionValueCasts.ShouldBe(1);
    }

    [Fact]
    public async Task ACastWhileTheTankIsAbove40Percent_IsNeverFlagged()
    {
        var analyzer = await Analyze(Info([]),
            Activation(500, Spells.Oblivion),
            Heal(501, Spells.Oblivion, TankId, 30_000),
            TankStagger(900, staggerHitPoints: 18_000),
            Activation(1_000, Spells.AmendFate, TankId),
            Heal(1_000, Spells.AmendFate, TankId, 1_000),
            TankStagger(1_050, staggerHitPoints: 12_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TankStaggerFraction.ShouldBe(0.45);
        cast.BelowOblivionValue.ShouldBe(true);
        cast.Flagged.ShouldBeFalse();
    }

    [Fact]
    public async Task AFreeCastThatAppliesEchoesOfDivinity_IsTheIntendedUse()
    {
        var analyzer = await Analyze(Info([AeonaTalents.EchoesOfDivinity, AeonaTalents.Uchronia]),
            Activation(500, Spells.Oblivion),
            Heal(501, Spells.Oblivion, TankId, 30_000),
            ApplyBuff(600, Spells.Uchronia),
            TankStagger(900, staggerHitPoints: 1_000),
            Activation(1_000, Spells.RestoreContinuity, TankId),
            RemoveBuff(1_000, Spells.Uchronia),
            Heal(1_000, Spells.RestoreContinuity, TankId, 500),
            ApplyBuff(1_000, Spells.EchoesOfDivinity, TankId),
            TankStagger(1_050, staggerHitPoints: 0),
            RemoveBuff(5_000, Spells.EchoesOfDivinity, TankId));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.WasFree.ShouldBeTrue();
        cast.AppliedEchoes.ShouldBeTrue();
        cast.OverwroteEchoes.ShouldBeFalse();
        cast.Flagged.ShouldBeFalse();
        analyzer.FreeCasts.ShouldBe(1);
        analyzer.FreeCastsOnRestoreContinuity.ShouldBe(1);
        analyzer.FreeCastsInPull.ShouldBe(1);
        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();
        echoes.Applications.ShouldBe(1);
        echoes.ActiveMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task ACastThatRefreshesARunningEchoesOfDivinity_DiscardsTheRemainder()
    {
        var analyzer = await Analyze(Info(Echoes),
            ApplyBuff(100, Spells.EchoesOfDivinity, TankId),
            RemoveBuff(4_100, Spells.EchoesOfDivinity, TankId),
            ApplyBuff(10_000, Spells.EchoesOfDivinity, TankId),
            TankStagger(11_900, staggerHitPoints: 1_000),
            Activation(12_000, Spells.AmendFate, TankId),
            Heal(12_000, Spells.AmendFate, TankId, 500),
            RefreshBuff(12_000, Spells.EchoesOfDivinity, TankId),
            RemoveBuff(16_000, Spells.EchoesOfDivinity, TankId));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.OverwroteEchoes.ShouldBeTrue();
        cast.EchoesOverwrittenMs.ShouldBe(2_000);
        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();
        echoes.Overwrites.ShouldBe(1);
        echoes.OverwrittenMs.ShouldBe(2_000);
        echoes.ActiveMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task ACleanseWithEntropyClaimOnCooldown_ProjectsTheTanksStaggerAtTheNextCharge()
    {
        var analyzer = await Analyze(Info([], AeonaLegendaries.MassEntropy),
            Completion(1_000, Spells.EntropyClaim),
            Completion(2_000, Spells.EntropyClaim, SecondEnemyId),
            Absorbed(3_000, Spells.AuraOfDeferredFate, TankId, 4_000),
            Absorbed(7_000, Spells.AuraOfDeferredFate, TankId, 4_000),
            TankStagger(9_900, staggerHitPoints: 8_000),
            Activation(10_000, Spells.AmendFate, TankId),
            Heal(10_000, Spells.AmendFate, TankId, 500),
            TankStagger(10_050, staggerHitPoints: 2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.EntropyClaimReadyInMs.ShouldBeGreaterThan(0);
        cast.EntropyClaimReadyInMs.ShouldBeLessThanOrEqualTo(12_000);
        cast.StaggerIntakePerSecond.ShouldNotBeNull().ShouldBe(1_000.0, 0.0001);
        cast.ProjectedStaggerFraction.ShouldNotBeNull().ShouldBeLessThan(StaggerCleanseAnalyzer.EntropicBurstHoldStaggerFraction);
        cast.CouldHaveWaited.ShouldBeTrue();
        analyzer.CastsWithEntropyClaimOnCooldown.ShouldBe(1);
        analyzer.CastsCouldHaveWaited.ShouldBe(1);
    }

    [Fact]
    public async Task ACleanseUnderHeavyIntake_CouldNotHaveWaited()
    {
        var analyzer = await Analyze(Info([], AeonaLegendaries.MassEntropy),
            Completion(1_000, Spells.EntropyClaim),
            Completion(2_000, Spells.EntropyClaim, SecondEnemyId),
            Absorbed(3_000, Spells.AuraOfDeferredFate, TankId, 20_000),
            Absorbed(7_000, Spells.AuraOfDeferredFate, TankId, 20_000),
            TankStagger(9_900, staggerHitPoints: 8_000),
            Activation(10_000, Spells.AmendFate, TankId),
            Heal(10_000, Spells.AmendFate, TankId, 500),
            TankStagger(10_050, staggerHitPoints: 2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.StaggerIntakePerSecond.ShouldNotBeNull().ShouldBe(5_000.0, 0.0001);
        cast.CouldHaveWaited.ShouldBeFalse();
        analyzer.CastsCouldHaveWaited.ShouldBe(0);
    }

    private static async Task<StaggerCleanseAnalyzer> Analyze(CombatantInfoEvent info, params Event[] events)
    {
        var parser = await AeonaLog.Analyze(BossPull(), [info, .. events]);
        return parser.StaggerCleanseAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<StaggerCleanseAnalyzer>();
    }
}
