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

public sealed class SearingBlazeSpreadAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 99;

    [Fact]
    public async Task Analyze_SpreadAcrossPartialPack_MeasuresCoverage()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Apply(targetId: 12, targetInstance: 0, timestamp: 2000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(5));

        var entry = parser.SearingBlazeAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<SearingBlazeSpreadAnalyzer>();
        analyzer.DistinctTargets.ShouldBe(3);
        analyzer.TargetCount.ShouldBe(5);
        analyzer.Coverage.ShouldBe(0.6, 0.0001);
        analyzer.TotalApplications.ShouldBe(3);

        var pull = entry.Pull;
        pull.SearingBlazeAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).SearingBlazeAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task Analyze_RefreshDoesNotInflateDistinct()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Refresh(targetId: 10, targetInstance: 0, timestamp: 2000),
            Refresh(targetId: 10, targetInstance: 0, timestamp: 2500),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(4));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(2);
        analyzer.TotalApplications.ShouldBe(2);
        analyzer.Coverage.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task Analyze_FullCoverage_ReachesEveryEnemy()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Apply(targetId: 12, targetInstance: 0, timestamp: 2000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(3));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(3);
        analyzer.Coverage.ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public async Task Analyze_NoSearingBlaze_ReportsZeroCoverage()
    {
        var (parser, _) = await AnalyzeAsync([], TrashFight(5));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(0);
        analyzer.Coverage.ShouldBe(0d, 0.0001);
        analyzer.TotalApplications.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_UnknownRoster_UsesReferenceFallback()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight());

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.TargetCount.ShouldBe(0);
        analyzer.DistinctTargets.ShouldBe(2);
        analyzer.Coverage.ShouldBe(2d / 3, 0.0001);
    }

    [Fact]
    public async Task Analyze_BossPull_IsNotEvaluated()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Apply(targetId: 12, targetInstance: 0, timestamp: 2000),
        };

        var (parser, _) = await AnalyzeAsync(events, BossFight());

        parser.SearingBlazeAnalyzers.Select(e => e.Analyzer).OfType<SearingBlazeSpreadAnalyzer>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_SameIdDistinctInstance_CountsSeparately()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 10, targetInstance: 1, timestamp: 1500),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(3));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_ForeignAbilityAndEnemySource_AreNotCounted()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            ForeignAbilityApply(targetId: 20, targetInstance: 0, timestamp: 1500),
            EnemySourcedApply(targetId: 30, targetInstance: 0, timestamp: 2000),
            ForeignAbilityRefresh(targetId: 40, targetInstance: 0, timestamp: 2500),
            EnemySourcedRefresh(targetId: 50, targetInstance: 0, timestamp: 3000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(5));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(1);
        analyzer.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_RefreshOnFreshTarget_CountsDistinct()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Refresh(targetId: 20, targetInstance: 0, timestamp: 1500),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(4));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(2);
        analyzer.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_LowCoverage_ReportsLowShare()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(10));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.DistinctTargets.ShouldBe(1);
        analyzer.Coverage.ShouldBe(0.1, 0.0001);
    }

    private static SearingBlazeSpreadAnalyzer SingleSpreadAnalyzer(ArdeosCombatLogParser parser) =>
        parser.SearingBlazeAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<SearingBlazeSpreadAnalyzer>();

    private static ApplyDebuffEvent Apply(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static RefreshDebuffEvent Refresh(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static ApplyDebuffEvent ForeignAbilityApply(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlaze.FSLID },
    };

    private static ApplyDebuffEvent EnemySourcedApply(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemyId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static RefreshDebuffEvent ForeignAbilityRefresh(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlaze.FSLID },
    };

    private static RefreshDebuffEvent EnemySourcedRefresh(int targetId, int? targetInstance, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemyId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static ReportFight TrashFight(int enemies) =>
        new(0, "", 0, null, 0, 20000, null, null, null, EnemyNpcs: [new FightNpc(1, 100, enemies, 1, null)]);

    private static ReportFight SpanningFight() =>
        new(0, "", 0, null, 0, 20000, null, null, null);

    private static ReportFight BossFight() =>
        new(0, "", 1, null, 0, 20000, null, null, null);

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
