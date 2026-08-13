using System.Linq;

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

public sealed class SearingBlazeUptimeAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BossId = 99;
    private const int AddId = 50;

    [Fact]
    public async Task Analyze_ReapplyAfterDrop_MeasuresUptimeAndGap()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Remove(BossId, 5000),
            Apply(BossId, 8000),
            Tick(BossId, 14000),
            Tick(BossId, 20000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var entry = parser.SearingBlazeAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<SearingBlazeUptimeAnalyzer>();
        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Uptime.ShouldBe(0.8, 0.0001);
        analyzer.GapCount.ShouldBe(1);
        analyzer.TotalGapMs.ShouldBe(3000);

        var pull = entry.Pull;
        pull.SearingBlazeAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).SearingBlazeAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task Analyze_OpenWindow_ClosesAtPullEnd()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Tick(BossId, 10000),
            Tick(BossId, 20000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.End.ShouldBe(20000);
        analyzer.Uptime.ShouldBe(0.95, 0.0001);
        analyzer.GapCount.ShouldBe(0);
        analyzer.TotalGapMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_OpenWindowStopsTicking_ClosesAtLastTick()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Tick(BossId, 6000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.End.ShouldBe(6000);
        analyzer.Uptime.ShouldBe(0.25, 0.0001);
        analyzer.GapCount.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_AddStopsTickingWithoutRemove_DoesNotOutrankBoss()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Apply(AddId, 2000),
            Tick(AddId, 3000),
            Remove(BossId, 9000),
            Apply(BossId, 12000),
            Tick(BossId, 16000),
            Tick(BossId, 20000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].Start.ShouldBe(1000);
        analyzer.Windows[0].End.ShouldBe(9000);
        analyzer.Windows[1].Start.ShouldBe(12000);
        analyzer.Windows[1].End.ShouldBe(20000);
        analyzer.Uptime.ShouldBe(0.8, 0.0001);
        analyzer.GapCount.ShouldBe(1);
        analyzer.TotalGapMs.ShouldBe(3000);
    }

    [Fact]
    public async Task Analyze_RefreshInsideWindow_ExtendsWithoutGap()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Refresh(BossId, 3000),
            Remove(BossId, 5000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1000);
        window.End.ShouldBe(5000);
        analyzer.GapCount.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0.2, 0.0001);
    }

    [Fact]
    public async Task Analyze_RefreshWithoutPriorApply_OpensWindow()
    {
        var events = new List<Event>
        {
            Refresh(BossId, 2000),
            Remove(BossId, 6000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(2000);
        window.End.ShouldBe(6000);
    }

    [Fact]
    public async Task Analyze_AddsCarryingDot_DoNotDiluteBossUptime()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1000),
            Apply(AddId, 3000),
            Remove(AddId, 5000),
            Remove(BossId, 11000),
        };

        var (parser, _) = await AnalyzeAsync(events);

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1000);
        window.End.ShouldBe(11000);
        analyzer.Uptime.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task Analyze_NoApplications_ReportsZero()
    {
        var (parser, _) = await AnalyzeAsync([]);

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Windows.ShouldBeEmpty();
        analyzer.Uptime.ShouldBe(0d, 0.0001);
        analyzer.GapCount.ShouldBe(0);
        analyzer.TotalGapMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrashPull_IsNotEvaluated()
    {
        var events = new List<Event> { Apply(BossId, 1000) };

        var (parser, _) = await AnalyzeAsync(events, TrashDungeon());

        parser.SearingBlazeAnalyzers.Select(e => e.Analyzer).OfType<SearingBlazeUptimeAnalyzer>().ShouldBeEmpty();
    }

    private static SearingBlazeUptimeAnalyzer SingleUptimeAnalyzer(ArdeosCombatLogParser parser) =>
        parser.SearingBlazeAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<SearingBlazeUptimeAnalyzer>();

    private static ApplyDebuffEvent Apply(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static RefreshDebuffEvent Refresh(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static RemoveDebuffEvent Remove(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static DamageEvent Tick(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = 1000,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static ReportDungeon BossDungeon() =>
        new(0, "", 1, null, 0, 20000, null, null, null);

    private static ReportDungeon TrashDungeon() =>
        new(0, "", 0, null, 0, 20000, null, null, null, EnemyNpcs: [new DungeonNpc(1, 100, 4, 1, null)]);

    private static async Task<(ArdeosCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        List<Event> events, ReportDungeon? dungeon = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, dungeon ?? BossDungeon());
        return (parser, result);
    }
}
