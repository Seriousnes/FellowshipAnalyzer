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
    public async Task ReadyWindowWithOvercap_SurfacesBothSignals()
    {
        var events = new List<Event> { Cast(Spells.Wildfire.FSLID, 1000) };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 100000));

        var analyzer = parser.EngulfingFlamesEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsReady.ShouldBe(1);
        analyzer.WastedCharges.ShouldBe(5);
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
