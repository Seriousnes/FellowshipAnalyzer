using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class EngulfingFlamesEconomyAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 99;

    [Fact]
    public void EngulfingFlames_IsCuratedAsTwoChargeTwentySecondSpell()
    {
        Spells.EngulfingFlames.Charges.ShouldBe(2);
        Spells.EngulfingFlames.Cooldown.ShouldBe(20d);
    }

    [Fact]
    public void Wildfire_IsCuratedAsSingleChargeFortyFiveSecondSpell()
    {
        Spells.Wildfire.Cooldown.ShouldBe(45d);
        Spells.Wildfire.Charges.ShouldBe(1);
        ((int)Spells.EngulfingFlamesDot.FSLID).ShouldBe(1002325);
    }

    [Fact]
    public async Task FirstWindow_AnchoredAtPullStart_BothEngulfingFlamesCastsSucceed()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.EngulfingFlames.FSLID, 600),
            Cast(Spells.Wildfire.FSLID, 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var entry = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(1);
        analyzer.DoubleAppliedWindows.ShouldBe(1);
        analyzer.MissedWindows.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ReadyTimestamp.ShouldBe(0);
        window.WindowStart.ShouldBe(0);
        window.EngulfingFlamesCastCount.ShouldBe(2);
        window.DoubleApplied.ShouldBeTrue();
        window.ChargesAtReady.ShouldBe(2);
        window.HeldMs.ShouldBe(1000);

        var pull = entry.Pull;
        pull.EngulfingFlamesEconomyAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).EngulfingFlamesEconomyAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task SecondWindow_EngulfingFlamesJustBeforeReady_CountsAndSucceeds()
    {
        var events = new List<Event>
        {
            Cast(Spells.Wildfire.FSLID, 1000),
            Cast(Spells.EngulfingFlames.FSLID, 44000),
            Cast(Spells.EngulfingFlames.FSLID, 45000),
            Cast(Spells.Wildfire.FSLID, 48000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 50000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(2);
        analyzer.Windows[0].EngulfingFlamesCastCount.ShouldBe(0);

        var second = analyzer.Windows[1];
        second.ReadyTimestamp.ShouldBe(46000);
        second.WindowStart.ShouldBe(40000);
        second.EngulfingFlamesCastCount.ShouldBe(2);
        second.DoubleApplied.ShouldBeTrue();
        second.EngulfingFlamesCasts.ShouldContain(44000);
        second.HeldMs.ShouldBe(2000);
    }

    [Fact]
    public async Task WildfireHeldLongAfterReady_LateDoubleCast_StillSucceeds()
    {
        var events = new List<Event>
        {
            Cast(Spells.Wildfire.FSLID, 1000),
            Cast(Spells.EngulfingFlames.FSLID, 78000),
            Cast(Spells.EngulfingFlames.FSLID, 79000),
            Cast(Spells.Wildfire.FSLID, 80000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 85000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.DoubleAppliedWindows.ShouldBe(1);

        var second = analyzer.Windows[1];
        second.ReadyTimestamp.ShouldBe(46000);
        second.HeldMs.ShouldBe(34000);
        second.EngulfingFlamesCastCount.ShouldBe(2);
        second.DoubleApplied.ShouldBeTrue();
    }

    [Fact]
    public async Task Window_WithSingleEngulfingFlamesCast_IsNotSuccessful()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.Wildfire.FSLID, 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.DoubleAppliedWindows.ShouldBe(0);
        analyzer.SingleApplicationWindows.ShouldBe(1);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.EngulfingFlamesCastCount.ShouldBe(1);
        window.DoubleApplied.ShouldBeFalse();
        window.ChargesAtReady.ShouldBe(2);
    }

    [Fact]
    public async Task Window_WithNoEngulfingFlamesCast_IsMissed()
    {
        var events = new List<Event> { Cast(Spells.Wildfire.FSLID, 1000) };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.MissedWindows.ShouldBe(1);
        analyzer.DoubleAppliedWindows.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.EngulfingFlamesCastCount.ShouldBe(0);
        window.ChargesAtReady.ShouldBe(2);
    }

    [Fact]
    public async Task ChargesAtReady_ReflectsEngulfingFlamesRechargeState()
    {
        var events = new List<Event>
        {
            Cast(Spells.Wildfire.FSLID, 500),
            Cast(Spells.EngulfingFlames.FSLID, 20000),
            Cast(Spells.EngulfingFlames.FSLID, 20100),
            Cast(Spells.Wildfire.FSLID, 45600),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 50000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Windows[0].ChargesAtReady.ShouldBe(2);

        var second = analyzer.Windows[1];
        second.ReadyTimestamp.ShouldBe(45500);
        second.ChargesAtReady.ShouldBe(1);
    }

    [Fact]
    public async Task ReadyTime_ReflectsAcrAcceleratedWildfireCooldown()
    {
        var events = new List<Event>
        {
            CombatantWithEmerald(1500),
            Cast(Spells.Wildfire.FSLID, 500),
            Cast(Spells.Wildfire.FSLID, 41000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 45000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(2);

        var second = analyzer.Windows[1];
        second.ReadyTimestamp.ShouldBe(40100);
        second.HeldMs.ShouldBe(900);
    }

    [Fact]
    public async Task NoWildfireWindows_EvaluatesNothing()
    {
        var (parser, _) = await AnalyzeAsync([], SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(0);
        analyzer.MissedWindows.ShouldBe(0);
        analyzer.Windows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Overcap_NeverCast_WastesEntirePull()
    {
        var (parser, _) = await AnalyzeAsync([], SpanningFight(0, 100000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(0);
        analyzer.CappedSeconds.ShouldBe(100d);
        analyzer.WastedCharges.ShouldBe(5);
    }

    [Fact]
    public async Task Overcap_RechargeThenIdle_WastesExactCharges()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 0),
            Cast(Spells.EngulfingFlames.FSLID, 100),
        };
        events.AddRange(Fillers(10000, 90000, 10000));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 100000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WastedCharges.ShouldBe(3);
        analyzer.CappedSeconds.ShouldBe(60d);
    }

    [Fact]
    public async Task Overcap_ChargesSpentBeforeRecharge_NoWaste()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 0),
            Cast(Spells.EngulfingFlames.FSLID, 100),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WastedCharges.ShouldBe(0);
        analyzer.CappedSeconds.ShouldBe(0d);
    }

    [Fact]
    public async Task Overcap_ActivelyCycledOverLongFight_NoWaste()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 0),
            Cast(Spells.EngulfingFlames.FSLID, 100),
            Cast(Spells.EngulfingFlames.FSLID, 20500),
            Cast(Spells.EngulfingFlames.FSLID, 40500),
            Cast(Spells.EngulfingFlames.FSLID, 60500),
        };
        events.AddRange(Fillers(5000, 75000, 5000));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 80000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WastedCharges.ShouldBe(0);
        analyzer.CappedSeconds.ShouldBe(0d);
    }

    [Fact]
    public async Task Overcap_WithLegendary_CountsAgainstAcceleratedRecharge()
    {
        var events = new List<Event> { CombatantWithLegendary() };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 200_000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.CappedSeconds.ShouldBe(200d);
        analyzer.WastedCharges.ShouldBe(11);
    }

    [Fact]
    public async Task MissedWindowWithOvercap_SurfacesBothSignals()
    {
        var events = new List<Event> { Cast(Spells.Wildfire.FSLID, 1000) };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 100000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(1);
        analyzer.MissedWindows.ShouldBe(1);
        analyzer.WastedCharges.ShouldBe(5);
    }

    [Fact]
    public async Task DevouringFlame_NotEquipped_ReportsFalseAndNoMetrics()
    {
        var events = new List<Event>
        {
            ApplyDebuff(1000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
            RemoveDebuff(5000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 10000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.DevouringFlameEquipped.ShouldBeFalse();
        analyzer.DevouringFlameAnyUptime.ShouldBe(0d);
        analyzer.DevouringFlameDoubleUptime.ShouldBe(0d);
        analyzer.DevouringFlameModeledBonusPercent.ShouldBe(0d);
    }

    [Fact]
    public async Task DevouringFlame_OverlappingEngulfingFlames_ProducesSubMetrics()
    {
        var events = new List<Event>
        {
            CombatantWithDevouringBracers(),
            ApplyDebuff(1000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(3000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
            RemoveDebuff(5000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
            RemoveDebuff(7000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 10000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.DevouringFlameEquipped.ShouldBeTrue();
        analyzer.DevouringFlameAnyUptime.ShouldBe(0.6, 0.0001);
        analyzer.DevouringFlameDoubleUptime.ShouldBe(0.2, 0.0001);
        analyzer.DevouringFlamePrimaryAverageInstances.ShouldBe(0.8, 0.0001);
        analyzer.DevouringFlameModeledBonusPercent.ShouldBe(4.8, 0.0001);
    }

    [Fact]
    public async Task DevouringFlame_EnemyDeathClosesWindow_FeedsUptime()
    {
        var events = new List<Event>
        {
            CombatantWithDevouringBracers(),
            ApplyDebuff(1000, EnemyId, Spells.EngulfingFlamesDot.FSLID),
            Death(4000, EnemyId),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 10000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.DevouringFlameEquipped.ShouldBeTrue();
        analyzer.DevouringFlameAnyUptime.ShouldBe(0.3, 0.0001);
        analyzer.DevouringFlameDoubleUptime.ShouldBe(0d);
    }

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveDebuffEvent RemoveDebuff(int timestamp, int targetId, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static DeathEvent Death(int timestamp, int targetId) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
    };

    /// <summary>
    /// Emerald power drives Ability Cooldown Reduction resolved on the <see cref="Combatant"/>: 450 unlocks
    /// 4% and 1500 (the gem power cap) unlocks 12%. Tests without a combatant info event get 0 power, so they
    /// see full-length cooldowns.
    /// </summary>
    private static CombatantInfoEvent CombatantWithEmerald(int power) => new()
    {
        SourceId = PlayerId,
        Emerald = power,
    };

    /// <summary>
    /// A combatant wearing a legendary item (quality tier 6), whose Strand of Eternity grants +10%
    /// cooldown acceleration via <see cref="CombatantStats.CooldownAcceleration"/>.
    /// </summary>
    private static CombatantInfoEvent CombatantWithLegendary() => new()
    {
        SourceId = PlayerId,
        Gear = [new Item { Id = 5999, Quality = 6 }],
    };

    /// <summary>
    /// A combatant wearing the legendary Draconic Bracers of the Devouring Flame (item 5225), which enable
    /// the Devouring Flame sub-metric.
    /// </summary>
    private static CombatantInfoEvent CombatantWithDevouringBracers() => new()
    {
        SourceId = PlayerId,
        Gear = [new Item { Id = FellowshipAnalyzer.Core.Common.Items.Items.DraconicBracersOfTheDevouringFlame.Id, Quality = 6 }],
    };

    private static IEnumerable<Event> Fillers(int start, int end, int interval)
    {
        for (var timestamp = start; timestamp <= end; timestamp += interval)
            yield return Filler(timestamp);
    }

    private static DamageEvent Filler(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = Spells.FireBallDot.FSLID },
        Amount = 1,
    };

    private static ReportFight SpanningFight(double startTime, double endTime) =>
        new(0, "", 0, null, startTime, endTime, null, null, null);

    private static async Task<(ArdeosCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
        return (parser, result);
    }
}
