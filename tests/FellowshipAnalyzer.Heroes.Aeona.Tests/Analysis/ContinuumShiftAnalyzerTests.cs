using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class ContinuumShiftAnalyzerTests
{
    private static readonly int[] Shift = [AeonaTalents.ContinuumShift];
    private static readonly int[] ShiftWithEdge = [AeonaTalents.ContinuumShift, AeonaTalents.EdgeOfRuin];

    [Fact]
    public async Task AWindowSpentOnTimeShard_TakesTheCastsDamageHealingAndOvercap()
    {
        var analyzer = await Analyze(Info(Shift),
            Completion(500, Spells.UnfoldingDoom),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Activation(1_500, Spells.TimeShard),
            Completion(3_000, Spells.TimeShard),
            Damage(3_100, Spells.TimeShardDamage, 10_000),
            Heal(3_100, Spells.TimeShard, TankId, 4_000, 1_000),
            Heal(3_100, Spells.TimeShard, AllyId, 0, 5_000),
            RemoveBuff(3_100, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.TimeShard);
        window.CastTimestamp.ShouldBe(3_000);
        window.Damage.ShouldBe(10_000);
        window.EffectiveHealing.ShouldBe(4_000);
        window.Overheal.ShouldBe(6_000);
        window.Justified.ShouldBeTrue();
        analyzer.Procs.ShouldBe(1);
        analyzer.SpentOnTimeShard.ShouldBe(1);
        analyzer.JustifiedSpends.ShouldBe(1);
        analyzer.UnfoldingDoomCasts.ShouldBe(1);
        analyzer.TimeShardDamage.ShouldBe(10_000);
        analyzer.TimeShardEffectiveHealing.ShouldBe(4_000);
    }

    [Fact]
    public async Task AWindowSpentOnEchoesOfRuinWithTheTankInDanger_IsJustified()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            TankStagger(2_900, staggerHitPoints: 1_000, hitPoints: 15_000),
            Completion(3_000, Spells.EchoesOfRuin),
            ApplyDebuff(3_050, Spells.EchoesOfRuinDot, EnemyId),
            ApplyDebuff(3_050, Spells.EchoesOfRuinDot, SecondEnemyId),
            RemoveBuff(3_050, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.EchoesOfRuin);
        window.EnemiesApplied.ShouldBe(2);
        window.TankHealthFraction.ShouldBe(0.375);
        window.Justified.ShouldBeTrue();
        analyzer.SpentOnEchoesOfRuin.ShouldBe(1);
        analyzer.JustifiedSpends.ShouldBe(1);
    }

    [Fact]
    public async Task AWindowSpentOnEchoesOfRuinIntoFleetingHourWithEdgeOfRuin_IsJustified()
    {
        var analyzer = await Analyze(Info(ShiftWithEdge),
            ApplyBuff(1_000, Spells.ContinuumShift),
            TankStagger(2_900, staggerHitPoints: 1_000, hitPoints: 30_000),
            Completion(3_000, Spells.EchoesOfRuin),
            RemoveBuff(3_050, Spells.ContinuumShift),
            Activation(10_000, Spells.FleetingHour, PlayerId));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.TankHealthFraction.ShouldBe(0.75);
        window.FleetingHourFollowed.ShouldBeTrue();
        window.Justified.ShouldBeTrue();
        analyzer.EdgeOfRuinTaken.ShouldBeTrue();
    }

    [Fact]
    public async Task AWindowSpentOnEchoesOfRuinWithAHealthyTankAndNoEdgeOfRuin_IsNotJustified()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            TankStagger(2_900, staggerHitPoints: 1_000, hitPoints: 30_000),
            Completion(3_000, Spells.EchoesOfRuin),
            RemoveBuff(3_050, Spells.ContinuumShift),
            Activation(10_000, Spells.FleetingHour, PlayerId));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.FleetingHourFollowed.ShouldBeFalse();
        window.Justified.ShouldBeFalse();
        analyzer.JustifiedSpends.ShouldBe(0);
    }

    [Fact]
    public async Task AWindowSpentOnEntropyClaim_IsNotJustified()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Completion(3_000, Spells.EntropyClaim),
            RemoveBuff(3_000, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.EntropyClaim);
        window.Justified.ShouldBeFalse();
        analyzer.SpentOnEntropyClaim.ShouldBe(1);
    }

    [Fact]
    public async Task AWindowRemovedWithNoCastBesideIt_IsLost()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            RemoveBuff(9_000, Spells.ContinuumShift),
            Completion(15_000, Spells.TimeShard));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.Lost);
        window.CastTimestamp.ShouldBeNull();
        analyzer.Lost.ShouldBe(1);
        analyzer.ClosedWindows.ShouldBe(1);
    }

    [Fact]
    public async Task ACastBeforeTheWindowOpened_DoesNotSpendIt()
    {
        var analyzer = await Analyze(Info(Shift),
            Completion(500, Spells.TimeShard),
            ApplyBuff(1_000, Spells.ContinuumShift),
            RemoveBuff(2_000, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.Lost);
        window.CastTimestamp.ShouldBeNull();
        analyzer.SpentOnTimeShard.ShouldBe(0);
    }

    [Fact]
    public async Task AWindowStillOpenAtThePullEnd_IsNeitherSpentNorLost()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.OpenAtPullEnd);
        window.End.ShouldBe(PullEnd);
        analyzer.ClosedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task AnActivationCast_DoesNotSpendTheWindow()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Activation(2_000, Spells.TimeShard),
            Completion(3_500, Spells.TimeShard),
            RemoveBuff(3_500, Spells.ContinuumShift));

        analyzer.Windows.ShouldHaveSingleItem().CastTimestamp.ShouldBe(3_500);
    }

    [Fact]
    public async Task AnInstantCast_SpendsTheWindow()
    {
        var analyzer = await Analyze(Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Activation(3_000, Spells.EchoesOfRuin),
            ApplyDebuff(3_000, Spells.EchoesOfRuinDot, EnemyId),
            ApplyDebuff(3_000, Spells.EchoesOfRuinDot, SecondEnemyId),
            RemoveBuff(3_002, Spells.ContinuumShift));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Spend.ShouldBe(ContinuumShiftSpend.EchoesOfRuin);
        window.CastTimestamp.ShouldBe(3_000);
        window.EnemiesApplied.ShouldBe(2);
        analyzer.Lost.ShouldBe(0);
    }

    private static async Task<ContinuumShiftAnalyzer> Analyze(CombatantInfoEvent info, params Event[] events)
    {
        var parser = await AeonaLog.Analyze(BossPull(), [info, .. events]);
        return parser.ContinuumShiftAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<ContinuumShiftAnalyzer>();
    }
}
