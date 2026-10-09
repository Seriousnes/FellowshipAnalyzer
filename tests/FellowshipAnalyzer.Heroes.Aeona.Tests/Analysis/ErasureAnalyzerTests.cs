using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class ErasureAnalyzerTests
{
    [Fact]
    public async Task WithoutTheTalent_NoAnalyzerIsConstructed()
    {
        var parser = await Analyze(Info([AeonaTalents.OblivionsEmbrace]),
            Activation(1_000, Spells.Oblivion),
            Damage(1_001, Spells.Oblivion, 5_000));

        parser.Erasure.ShouldBeNull();
    }

    [Fact]
    public async Task Ticks_TakeTheHealsAndShieldsBeforeThem_ApartFromDirectHits()
    {
        var parser = await Analyze(Info([AeonaTalents.OblivionsEmbrace, AeonaTalents.Erasure]),
            Activation(1_000, Spells.Oblivion),
            Heal(1_001, Spells.Oblivion, TankId, 3_000, 1_000),
            ApplyBuff(1_001, Spells.OblivionAbsorbAbsorb, TankId, absorb: 250),
            Damage(1_001, Spells.Oblivion, 6_000),
            Heal(2_300, Spells.Oblivion, TankId, 500, 400),
            RemoveBuff(2_300, Spells.OblivionAbsorbAbsorb, TankId, absorb: 200),
            ApplyBuff(2_300, Spells.OblivionAbsorbAbsorb, TankId, absorb: 300),
            Heal(2_300, Spells.Oblivion, AllyId, 500),
            Damage(2_300, Spells.Erasure, 1_200, tick: true),
            Heal(2_300, Spells.Oblivion, SecondAllyId, 250),
            Damage(2_300, Spells.Erasure, 800, SecondEnemyId, tick: true));

        var erasure = parser.Erasure.ShouldNotBeNull();
        erasure.Ticks.ShouldBe(2);
        erasure.Damage.ShouldBe(2_000);
        erasure.EffectiveHealing.ShouldBe(1_250);
        erasure.Overheal.ShouldBe(400);
        erasure.ShieldApplied.ShouldBe(100);
        erasure.DirectDamage.ShouldBe(6_000);
        erasure.DirectEffectiveHealing.ShouldBe(3_000);
        erasure.DirectShieldApplied.ShouldBe(250);
        erasure.DamageShare.ShouldBe(0.25, 0.0001);
        erasure.HealingShare.ShouldBe(1_250 / 4_250.0, 0.0001);
        erasure.ShieldShare.ShouldNotBeNull().ShouldBe(100 / 350.0, 0.0001);
        erasure.StatisticCategory.ShouldBe(StatisticCategory.Talents);
        erasure.Statistic.ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutOblivionsEmbrace_ShieldingIsNull()
    {
        var parser = await Analyze(Info([AeonaTalents.Erasure]),
            Heal(2_300, Spells.Oblivion, TankId, 500),
            Damage(2_300, Spells.Erasure, 1_200, tick: true));

        var erasure = parser.Erasure.ShouldNotBeNull();
        erasure.ShieldApplied.ShouldBeNull();
        erasure.ShieldShare.ShouldBeNull();
    }

    private static Task<AeonaCombatLogParser> Analyze(CombatantInfoEvent info, params Event[] events) =>
        AeonaLog.Analyze(BossPull(), [info, .. events]);
}
