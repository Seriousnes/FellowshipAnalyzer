using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class OblivionAnalyzerTests
{
    private static readonly int[] Embrace = [AeonaTalents.OblivionsEmbrace];
    private static readonly int[] EmbraceAndErasure = [AeonaTalents.OblivionsEmbrace, AeonaTalents.Erasure];

    [Fact]
    public async Task ACast_TakesItsHealsShieldsAndDamageFromTheNextMillisecond()
    {
        var analyzer = await Analyze(Info(Embrace),
            Activation(1_000, Spells.Oblivion),
            Heal(1_001, Spells.Oblivion, TankId, 2_405),
            Heal(1_001, Spells.Oblivion, AllyId, 0, 2_404),
            ApplyBuff(1_001, Spells.OblivionAbsorbAbsorb, AllyId, absorb: 601),
            Heal(1_001, Spells.Oblivion, SecondAllyId, 0, 2_404),
            ApplyBuff(1_001, Spells.OblivionAbsorbAbsorb, SecondAllyId, absorb: 601),
            Damage(1_001, Spells.OblivionDamage, 4_808));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Target.ShouldBe(new UnitKey(EnemyId, 0));
        cast.EffectiveHealing.ShouldBe(2_405);
        cast.Overheal.ShouldBe(4_808);
        cast.AlliesHealed.ShouldBe(3);
        cast.ShieldApplied.ShouldBe(1_202);
        cast.AlliesShielded.ShouldBe(2);
        cast.Damage.ShouldBe(4_808);
        analyzer.ValuePerCast.ShouldNotBeNull().ShouldBe(3_607.0, 0.0001);
        analyzer.ShieldAppliedPerCast.ShouldNotBeNull().ShouldBe(1_202.0, 0.0001);
        analyzer.ErasureEffectiveHealingPerCast.ShouldBeNull();
        analyzer.ErasureDamagePerCast.ShouldBeNull();
    }

    [Fact]
    public async Task AnErasureTickInsideTheAttributionWindow_IsCountedForErasureAndNotTheCast()
    {
        var analyzer = await Analyze(Info(EmbraceAndErasure),
            Activation(1_000, Spells.Oblivion),
            Heal(1_001, Spells.Oblivion, TankId, 2_000),
            Damage(1_001, Spells.Oblivion, 5_000),
            Heal(1_020, Spells.Oblivion, TankId, 300, 200),
            RemoveBuff(1_020, Spells.OblivionAbsorbAbsorb, AllyId, absorb: 400),
            ApplyBuff(1_020, Spells.OblivionAbsorbAbsorb, AllyId, absorb: 450),
            Heal(1_020, Spells.Oblivion, AllyId, 0, 200),
            Damage(1_020, Spells.Erasure, 1_000, tick: true));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.EffectiveHealing.ShouldBe(2_000);
        cast.Overheal.ShouldBe(0);
        cast.ShieldApplied.ShouldBe(0);
        cast.Damage.ShouldBe(5_000);
        analyzer.ErasureDamage.ShouldBe(1_000);
        analyzer.ErasureEffectiveHealing.ShouldBe(300);
        analyzer.ErasureOverheal.ShouldBe(400);
        analyzer.ErasureShieldApplied.ShouldBe(50);
        analyzer.ErasureEffectiveHealingPerCast.ShouldNotBeNull().ShouldBe(300.0, 0.0001);
        analyzer.ErasureShieldAppliedPerCast.ShouldNotBeNull().ShouldBe(50.0, 0.0001);
        analyzer.ErasureDamagePerCast.ShouldNotBeNull().ShouldBe(1_000.0, 0.0001);
        analyzer.ValuePerCast.ShouldNotBeNull().ShouldBe(2_350.0, 0.0001);
    }

    [Fact]
    public async Task AShieldReplacingARemainder_CountsOnlyTheAbsorbItAdds()
    {
        var analyzer = await Analyze(Info(Embrace),
            Activation(1_000, Spells.Oblivion),
            Heal(1_001, Spells.Oblivion, AllyId, 0, 2_000),
            RemoveBuff(1_001, Spells.OblivionAbsorbAbsorb, AllyId, absorb: 900),
            ApplyBuff(1_001, Spells.OblivionAbsorbAbsorb, AllyId, absorb: 1_400),
            Heal(1_001, Spells.Oblivion, SecondAllyId, 0, 2_000),
            RemoveBuff(1_001, Spells.OblivionAbsorbAbsorb, SecondAllyId, absorb: 1_500),
            ApplyBuff(1_001, Spells.OblivionAbsorbAbsorb, SecondAllyId, absorb: 1_500),
            Damage(1_001, Spells.Oblivion, 4_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ShieldApplied.ShouldBe(500);
        cast.AlliesShielded.ShouldBe(1);
    }

    [Fact]
    public async Task HealsWithNoHitAfterThem_AreNotCredited()
    {
        var analyzer = await Analyze(Info(Embrace),
            Activation(1_000, Spells.Oblivion),
            Heal(1_001, Spells.Oblivion, TankId, 2_000),
            Activation(3_000, Spells.Oblivion),
            Heal(3_001, Spells.Oblivion, TankId, 1_000),
            Damage(3_001, Spells.Oblivion, 4_000));

        analyzer.Casts.Select(cast => cast.EffectiveHealing).ShouldBe([0L, 1_000L]);
    }

    [Fact]
    public async Task ACastAbove40PercentStaggerWithACleanseReady_IsAtCleansePriority()
    {
        var analyzer = await Analyze(Info(Embrace),
            TankStagger(900, staggerHitPoints: 18_000),
            Activation(1_000, Spells.Oblivion));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TankStaggerFraction.ShouldBe(0.45);
        cast.CleanseAvailable.ShouldBeTrue();
        cast.AtCleansePriority.ShouldBeTrue();
        analyzer.CastsRated.ShouldBe(1);
        analyzer.CastsAtCleansePriority.ShouldBe(1);
    }

    [Fact]
    public async Task ACastBelow40PercentStagger_IsNotAtCleansePriority()
    {
        var analyzer = await Analyze(Info(Embrace),
            TankStagger(900, staggerHitPoints: 12_000),
            Activation(1_000, Spells.Oblivion));

        analyzer.Casts.ShouldHaveSingleItem().AtCleansePriority.ShouldBeFalse();
        analyzer.CastsAtCleansePriority.ShouldBe(0);
    }

    [Fact]
    public async Task ACastWithStaleStagger_IsNotRated()
    {
        var analyzer = await Analyze(Info(Embrace),
            TankStagger(100, staggerHitPoints: 18_000),
            Activation(2_000, Spells.Oblivion));

        analyzer.Casts.ShouldHaveSingleItem().Rated.ShouldBeFalse();
        analyzer.CastsRated.ShouldBe(0);
    }

    [Fact]
    public async Task Targets_GroupCastsByTheEnemyTheyWereCastInto()
    {
        var analyzer = await Analyze(Info(Embrace),
            TankStagger(900, staggerHitPoints: 18_000),
            Activation(1_000, Spells.Oblivion, EnemyId),
            Damage(1_001, Spells.OblivionDamage, 4_000, EnemyId),
            Activation(3_000, Spells.Oblivion, SecondEnemyId),
            Damage(3_001, Spells.OblivionDamage, 3_000, SecondEnemyId),
            Activation(5_000, Spells.Oblivion, SecondEnemyId),
            Damage(5_001, Spells.OblivionDamage, 3_000, SecondEnemyId));

        analyzer.Targets.Select(target => (target.Unit.ActorId, target.Casts, target.Damage)).ShouldBe(
        [
            (SecondEnemyId, 2, 6_000L),
            (EnemyId, 1, 4_000L),
        ]);
        analyzer.Targets[0].CastsAtCleansePriority.ShouldBe(0);
        analyzer.Targets[1].CastsAtCleansePriority.ShouldBe(1);
    }

    [Fact]
    public async Task AFreeCast_ReadsItsSourceFromTheUchroniaWindow()
    {
        var analyzer = await Analyze(Info([AeonaTalents.OblivionsEmbrace, AeonaTalents.Uchronia]),
            ApplyBuff(500, Spells.Uchronia),
            Activation(1_000, Spells.Oblivion),
            RemoveBuff(1_000, Spells.Uchronia));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.WasFree.ShouldBeTrue();
        cast.FreeCastSource.ShouldBe(FreeCastSource.Uchronia);
        analyzer.FreeCasts.ShouldBe(1);
        analyzer.FreeCastOpportunities.ShouldBe(1);
    }

    private static async Task<OblivionAnalyzer> Analyze(CombatantInfoEvent info, params Event[] events)
    {
        var parser = await AeonaLog.Analyze(BossPull(), [info, .. events]);
        return parser.OblivionAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<OblivionAnalyzer>();
    }
}
