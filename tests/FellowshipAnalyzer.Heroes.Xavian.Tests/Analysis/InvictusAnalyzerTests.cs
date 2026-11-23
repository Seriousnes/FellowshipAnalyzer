using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Xavian.Analysis;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

using Shouldly;

using Xunit;

using CoreSpells = FellowshipAnalyzer.Core.Common.Spells.Spells;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Xavian.Talents;

using static FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis.XavianAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class InvictusAnalyzerTests
{
    private const int LegendaryQuality = 6;
    private const double Tolerance = 0.000001;

    private static readonly double HastePerSpiritOfHeroism = StatBuffs.Percentages[CoreSpells.SpiritOfHeroismNotTank.FSLID].Haste!.AsT0;

    private static readonly double Coefficient = EffectData.ModifiersOf(Spells.InvictusCooldownRecovery.Id)
        .ToArray()
        .Single(modifier => modifier.Attribute == StatAttribute.CooldownRecovery)
        .Coefficient!.Value;

    private static ApplyDebuffEvent Heroism(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static CombatantInfoEvent Combatant(bool legendary, params Talent[] talents)
    {
        var combatant = XavianAnalysisFixture.Combatant(talents);
        if (legendary) combatant.Gear = [new Item { Id = 999, Quality = LegendaryQuality }];
        return combatant;
    }

    private static double Recovery(XavianCombatLogParser parser) =>
        parser.GetModule<StatTracker>()!.CurrentCooldownRecovery;

    [Fact]
    public void TheCoefficient_IsOneThirdOfHaste() => Coefficient.ShouldBe(0.33334, Tolerance);

    [Fact]
    public async Task SolarShieldOnThePlayer_RaisesCooldownRecoveryByTheHasteTerm()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus), Heroism(500, CoreSpells.SpiritOfHeroismNotTank), ApplyBuff(10_000, Spells.SolarShieldAbsorb));

        Recovery(parser).ShouldBe(1.0 + Coefficient * HastePerSpiritOfHeroism, Tolerance);
        Recovery(parser).ShouldBe(1.100002, Tolerance);
    }

    [Fact]
    public async Task WithALegendaryItem_TheHasteTermAddsToTheLegendaryRecovery()
    {
        var parser = await AnalyzeAs(Combatant(true, Talents.Invictus), Heroism(500, CoreSpells.SpiritOfHeroismNotTank), ApplyBuff(10_000, Spells.SolarShieldAbsorb));

        Recovery(parser).ShouldBe(1.200002, Tolerance);
    }

    [Fact]
    public async Task SolarShieldOnAnAllyOnly_RaisesCooldownRecovery()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus), Heroism(500, CoreSpells.SpiritOfHeroismNotTank), ApplyBuff(10_000, Spells.SolarShieldAbsorb, AllyId));

        Recovery(parser).ShouldBe(1.100002, Tolerance);
    }

    [Fact]
    public async Task SolarShieldOnThePlayerAndAnAlly_EndsWithTheLastRemoval()
    {
        var combatant = Combatant(false, Talents.Invictus);
        var heroism = Heroism(500, CoreSpells.SpiritOfHeroismNotTank);

        var playerRemoved = await AnalyzeAs(combatant, heroism,
            ApplyBuff(10_000, Spells.SolarShieldAbsorb),
            ApplyBuff(11_000, Spells.SolarShieldAbsorb, AllyId),
            RemoveBuff(15_000, Spells.SolarShieldAbsorb));
        var bothRemoved = await AnalyzeAs(combatant, heroism,
            ApplyBuff(10_000, Spells.SolarShieldAbsorb),
            ApplyBuff(11_000, Spells.SolarShieldAbsorb, AllyId),
            RemoveBuff(15_000, Spells.SolarShieldAbsorb),
            RemoveBuff(18_000, Spells.SolarShieldAbsorb, AllyId));

        Recovery(playerRemoved).ShouldBe(1.100002, Tolerance);
        Recovery(bothRemoved).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public async Task HasteRisingDuringTheWindow_ReplacesTheTerm()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus),
            Heroism(500, CoreSpells.SpiritOfHeroismNotTank),
            ApplyBuff(10_000, Spells.SolarShieldAbsorb),
            Heroism(14_000, CoreSpells.SpiritOfHeroismHealer));

        Recovery(parser).ShouldBe(1.0 + Coefficient * 2 * HastePerSpiritOfHeroism, Tolerance);
        Recovery(parser).ShouldBe(1.200004, Tolerance);
    }

    [Fact]
    public async Task WithoutTheTalent_SolarShieldLeavesCooldownRecoveryAtOne()
    {
        var parser = await AnalyzeAs(Combatant(false), Heroism(500, CoreSpells.SpiritOfHeroismNotTank), ApplyBuff(10_000, Spells.SolarShieldAbsorb));

        Recovery(parser).ShouldBe(1.0, Tolerance);
        parser.GetModule<InvictusAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task BlindingSlash_RecoversFasterInsideTheWindowOnly()
    {
        var combatant = Combatant(false, Talents.Invictus);
        var heroism = Heroism(500, CoreSpells.SpiritOfHeroismNotTank);
        var cast = Cast(5_000, Spells.BlindingSlash);

        var inside = await AnalyzeAs(combatant, heroism, cast, ApplyBuff(10_000, Spells.SolarShieldAbsorb));
        var outside = await AnalyzeAs(combatant, heroism, cast, ApplyBuff(10_000, Spells.SolarShieldAbsorb), RemoveBuff(18_000, Spells.SolarShieldAbsorb));

        inside.GetModule<SpellUsable>()!.EffectiveRate(Spells.BlindingSlash.FSLID).ShouldBe(1.100002, Tolerance);
        outside.GetModule<SpellUsable>()!.EffectiveRate(Spells.BlindingSlash.FSLID).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public async Task AnAbilityWithNoCooldownScaling_IgnoresTheTerm()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus),
            Heroism(500, CoreSpells.SpiritOfHeroismNotTank),
            Cast(5_000, Spells.Interrupt),
            ApplyBuff(10_000, Spells.SolarShieldAbsorb));

        parser.GetModule<SpellUsable>()!.EffectiveRate(Spells.Interrupt.FSLID).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public async Task Windows_SpanTheShieldAndUptimeSumsThem()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus),
            ApplyBuff(10_000, Spells.SolarShieldAbsorb),
            RemoveBuff(18_000, Spells.SolarShieldAbsorb));

        var analyzer = parser.GetModule<InvictusAnalyzer>()!;

        analyzer.Windows.ShouldBe([new InvictusWindow(10_000, 18_000)]);
        analyzer.UptimeMs.ShouldBe(8_000);
    }

    [Fact]
    public async Task AnOpenWindow_EndsWithTheDungeon()
    {
        var parser = await AnalyzeAs(Combatant(false, Talents.Invictus), ApplyBuff(10_000, Spells.SolarShieldAbsorb));

        var analyzer = parser.GetModule<InvictusAnalyzer>()!;

        analyzer.Windows.ShouldBe([new InvictusWindow(10_000, (int)BossDungeon.EndTime)]);
    }
}
