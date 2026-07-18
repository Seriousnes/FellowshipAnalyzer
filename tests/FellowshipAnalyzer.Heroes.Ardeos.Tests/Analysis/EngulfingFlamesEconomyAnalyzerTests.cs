using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;

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
    public async Task WildfireWindow_WithBothCharges_IsReady()
    {
        var events = new List<Event> { Cast(Spells.Wildfire.FSLID, 1000) };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var entry = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(1);
        analyzer.WindowsReady.ShouldBe(1);
        analyzer.WindowsShort.ShouldBe(0);
        analyzer.WastedCharges.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ChargesAvailable.ShouldBe(2);
        window.Ready.ShouldBeTrue();

        var pull = entry.Pull;
        pull.EngulfingFlamesEconomyAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).EngulfingFlamesEconomyAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task WildfireWindow_WithBothChargesSpent_IsShort()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.EngulfingFlames.FSLID, 600),
            Cast(Spells.Wildfire.FSLID, 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(1);
        analyzer.WindowsReady.ShouldBe(0);
        analyzer.WindowsShort.ShouldBe(1);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ChargesAvailable.ShouldBe(0);
        window.Ready.ShouldBeFalse();
    }

    [Fact]
    public async Task WildfireWindow_WithOneCharge_IsShort()
    {
        var events = new List<Event>
        {
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.Wildfire.FSLID, 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsReady.ShouldBe(0);
        analyzer.WindowsShort.ShouldBe(1);

        analyzer.Windows.ShouldHaveSingleItem().ChargesAvailable.ShouldBe(1);
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
        // A legendary's Strand of Eternity accelerates the 20s recharge to ~18.2s, so a 200s pull spent
        // entirely at max charges wastes 11 recharge periods, one more than the raw curated 20s reports.
        var events = new List<Event> { CombatantWithLegendary() };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 200_000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.CappedSeconds.ShouldBe(200d);
        analyzer.WastedCharges.ShouldBe(11);
    }

    [Fact]
    public async Task ReadyWindowWithOvercap_SurfacesBothSignals()
    {
        var events = new List<Event> { Cast(Spells.Wildfire.FSLID, 1000) };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 100000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsReady.ShouldBe(1);
        analyzer.WastedCharges.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // Ability Cooldown Reduction (Emerald "Blessing of the Commander")
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WithoutGemPower_EngulfingFlamesRechargesAtFullTwentySeconds()
    {
        // Both charges spent at the pull; at 36s the second is still recharging (0.5s + 20s + 20s =
        // 40.5s), so Wildfire finds only one charge.
        var events = new List<Event>
        {
            CombatantWithEmerald(0),
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.EngulfingFlames.FSLID, 600),
            Cast(Spells.Wildfire.FSLID, 36_000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 45_000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Windows.ShouldHaveSingleItem().ChargesAvailable.ShouldBe(1);
        analyzer.WindowsShort.ShouldBe(1);
    }

    [Fact]
    public async Task EmeraldAtCap_RechargesEngulfingFlamesFastEnoughToArmTheWindow()
    {
        // Identical timings, but 12% ACR shortens each recharge to 17.6s, so both charges are back by
        // 35.7s and the same Wildfire window is armed. This is the whole point of modelling ACR: it
        // moves windows from short to ready.
        var events = new List<Event>
        {
            CombatantWithEmerald(1500),
            Cast(Spells.EngulfingFlames.FSLID, 500),
            Cast(Spells.EngulfingFlames.FSLID, 600),
            Cast(Spells.Wildfire.FSLID, 36_000),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 45_000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Windows.ShouldHaveSingleItem().ChargesAvailable.ShouldBe(2);
        analyzer.WindowsReady.ShouldBe(1);
        analyzer.WindowsShort.ShouldBe(0);
    }

    [Fact]
    public async Task NoWildfireWindows_EvaluatesNothing()
    {
        var (parser, _) = await AnalyzeAsync([], SpanningFight(0, 15000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEvaluated.ShouldBe(0);
        analyzer.WindowsShort.ShouldBe(0);
        analyzer.Windows.ShouldBeEmpty();
    }

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    /// <summary>
    /// Emerald power drives Ability Cooldown Reduction via <see cref="GemPowers"/>: 450 unlocks 4% and
    /// 1500 (the gem power cap) unlocks 12%. Tests without a combatant info event get 0 power, so they
    /// see full-length cooldowns.
    /// </summary>
    private static CombatantInfoEvent CombatantWithEmerald(int power) => new()
    {
        SourceId = PlayerId,
        Emerald = power,
    };

    /// <summary>
    /// A combatant wearing a legendary item (quality tier 6), whose Strand of Eternity grants +10%
    /// cooldown acceleration via <see cref="Core.Analysis.GearCooldownRecovery"/>.
    /// </summary>
    private static CombatantInfoEvent CombatantWithLegendary() => new()
    {
        SourceId = PlayerId,
        Gear = [new Item { Id = 5222, Quality = 6 }],
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
