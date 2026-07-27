using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class RendAnalyzerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int AddId = 50;
    private const int EnemySourceId = 77;
    private const int PullEnd = 20_000;

    [Fact]
    public async Task Analyze_BossPull_MeasuresUptimeGapsAndWindows()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1_000),
            Apply(AddId, 2_000),
            Remove(AddId, 3_000),
            Remove(BossId, 5_000),
            Apply(BossId, 8_000),
            StackGained(BossId, 14_000),
            StackGained(BossId, PullEnd),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        var entry = parser.RendAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<RendUptimeAnalyzer>();

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].Start.ShouldBe(1_000);
        analyzer.Windows[0].End.ShouldBe(5_000);
        analyzer.Windows[1].Start.ShouldBe(8_000);
        analyzer.Windows[1].End.ShouldBe(PullEnd);
        analyzer.Uptime.ShouldBe(0.8, 0.0001);
        analyzer.GapCount.ShouldBe(1);
        analyzer.TotalGapMs.ShouldBe(3_000);

        var pull = entry.Pull;
        pull.RendAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).RendAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task Analyze_BossPull_AddStopsEmittingWithoutRemove_DoesNotOutrankBoss()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1_000),
            Apply(AddId, 2_000),
            StackGained(AddId, 3_000),
            Remove(BossId, 9_000),
            Apply(BossId, 12_000),
            StackGained(BossId, 16_000),
            StackGained(BossId, PullEnd),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].Start.ShouldBe(1_000);
        analyzer.Windows[0].End.ShouldBe(9_000);
        analyzer.Windows[1].Start.ShouldBe(12_000);
        analyzer.Windows[1].End.ShouldBe(PullEnd);
        analyzer.Uptime.ShouldBe(0.8, 0.0001);
        analyzer.GapCount.ShouldBe(1);
        analyzer.TotalGapMs.ShouldBe(3_000);
    }

    [Fact]
    public async Task Analyze_BossPull_StacksRunToPullEnd_KeepsWindowOpenToPullEnd()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1_000),
            StackGained(BossId, 10_000),
            StackLost(BossId, PullEnd),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(PullEnd);
        analyzer.Uptime.ShouldBe(0.95, 0.0001);
        analyzer.GapCount.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_TargetStopsEmitting_ClosesWindowAtLastEvent()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1_000),
            StackGained(BossId, 6_000),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.End.ShouldBe(6_000);
        analyzer.Uptime.ShouldBe(0.25, 0.0001);
        analyzer.GapCount.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_RefreshInsideWindow_DoesNotSplitIt()
    {
        var events = new List<Event>
        {
            Apply(BossId, 1_000),
            Refresh(BossId, 3_000),
            Remove(BossId, 5_000),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(5_000);
        analyzer.GapCount.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0.2, 0.0001);
    }

    [Fact]
    public async Task Analyze_BossPull_NoApplications_ReportsZero()
    {
        var (parser, _) = await AnalyzeAsync([], BossFight());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Windows.ShouldBeEmpty();
        analyzer.Uptime.ShouldBe(0d, 0.0001);
        analyzer.GapCount.ShouldBe(0);
        analyzer.TotalGapMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_SpreadIsNotEvaluated()
    {
        var events = new List<Event> { Apply(BossId, 1_000) };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        parser.RendAnalyzers.Select(e => e.Analyzer).OfType<RendSpreadAnalyzer>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_MultiPull_CountsDistinctTargetsAndFreshApplications()
    {
        var events = new List<Event>
        {
            Apply(10, 1_000),
            Apply(11, 1_500),
            Apply(12, 2_000),
            Refresh(10, 2_500),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(4));

        var entry = parser.RendAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<RendSpreadAnalyzer>();

        analyzer.DistinctTargets.ShouldBe(3);
        analyzer.TotalApplications.ShouldBe(3);
        analyzer.TargetCount.ShouldBe(4);
        analyzer.Coverage.ShouldBe(0.75, 0.0001);

        var pull = entry.Pull;
        pull.RendAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).RendAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task Analyze_MultiPull_UnknownRoster_UsesReferenceFallback()
    {
        var events = new List<Event>
        {
            Apply(10, 1_000),
            Apply(11, 1_500),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight());

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.TargetCount.ShouldBe(0);
        analyzer.DistinctTargets.ShouldBe(2);
        analyzer.Coverage.ShouldBe(2d / 3, 0.0001);
    }

    [Fact]
    public async Task Analyze_MultiPull_SameIdDistinctInstance_CountsSeparately()
    {
        var events = new List<Event>
        {
            Apply(10, 1_000, targetInstance: 0),
            Apply(10, 1_500, targetInstance: 1),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(3));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(2);
        analyzer.TotalApplications.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_MultiPull_EnemySourcedBleed_IsNotCounted()
    {
        var events = new List<Event>
        {
            Apply(10, 1_000),
            EnemySourcedApply(20, 1_500),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(4));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(1);
        analyzer.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_MultiPull_NoApplications_ReportsZeroCoverage()
    {
        var (parser, _) = await AnalyzeAsync([], TrashFight(5));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(0);
        analyzer.TotalApplications.ShouldBe(0);
        analyzer.Coverage.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task Analyze_MultiPull_UptimeIsNotEvaluated()
    {
        var events = new List<Event> { Apply(10, 1_000) };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(4));

        parser.RendAnalyzers.Select(e => e.Analyzer).OfType<RendUptimeAnalyzer>().ShouldBeEmpty();
    }

    private static RendUptimeAnalyzer SingleUptimeAnalyzer(GundeCombatLogParser parser) =>
        parser.RendAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<RendUptimeAnalyzer>();

    private static RendSpreadAnalyzer SingleSpreadAnalyzer(GundeCombatLogParser parser) =>
        parser.RendAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<RendSpreadAnalyzer>();

    private static ApplyDebuffEvent Apply(int targetId, int timestamp, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static RefreshDebuffEvent Refresh(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static RemoveDebuffEvent Remove(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static ApplyDebuffStackEvent StackGained(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Stack = 2,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static RemoveDebuffStackEvent StackLost(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Stack = 1,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static ApplyDebuffEvent EnemySourcedApply(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemySourceId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static ReportFight BossFight() =>
        new(0, "Boss", 1, null, 0, PullEnd, null, null, null);

    private static ReportFight TrashFight(int enemies) =>
        new(0, "Trash", 0, null, 0, PullEnd, null, null, null, EnemyNpcs: [new FightNpc(1, 100, enemies, null, null)]);

    private static ReportFight SpanningFight() =>
        new(0, "Trash", 0, null, 0, PullEnd, null, null, null);

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
        return (parser, result);
    }
}
