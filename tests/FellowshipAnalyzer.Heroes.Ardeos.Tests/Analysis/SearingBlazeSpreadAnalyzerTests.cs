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

public sealed class SearingBlazeSpreadAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 99;

    [Fact]
    public async Task Analyze_SpreadAcrossPartialPack_ScoresCoverage()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Apply(targetId: 12, targetInstance: 0, timestamp: 2000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(5));

        var entry = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem();
        var report = entry.Result;
        report.DistinctTargets.ShouldBe(3);
        report.TargetCount.ShouldBe(5);
        report.Coverage.ShouldBe(0.6, 0.0001);
        report.TotalApplications.ShouldBe(3);
        report.ScoreCard.Score.ShouldBe(60);
        report.ScoreCard.Accent.ShouldBe("amber");
        report.Findings[0].Severity.ShouldBe("warning");

        var pull = entry.Pull;
        pull.SearingBlazeSpreadReport.ShouldBe(report);
        parser.For(pull).SearingBlazeSpreadReport.ShouldBe(report);
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

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(2);
        report.TotalApplications.ShouldBe(2);
        report.ScoreCard.Score.ShouldBe(50);
        report.ScoreCard.Accent.ShouldBe("amber");
    }

    [Fact]
    public async Task Analyze_FullCoverage_ScoresIce()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
            Apply(targetId: 11, targetInstance: 0, timestamp: 1500),
            Apply(targetId: 12, targetInstance: 0, timestamp: 2000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(3));

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(3);
        report.Coverage.ShouldBe(1.0, 0.0001);
        report.ScoreCard.Score.ShouldBe(100);
        report.ScoreCard.Accent.ShouldBe("ice");
    }

    [Fact]
    public async Task Analyze_NoSearingBlaze_InfoFindingZeroScore()
    {
        var (parser, _) = await AnalyzeAsync([], TrashFight(5));

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(0);
        report.ScoreCard.Score.ShouldBe(0);
        report.ScoreCard.Accent.ShouldBe("ember");
        report.Findings.ShouldNotBeEmpty();
        report.Findings[0].Severity.ShouldBe("info");
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

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.TargetCount.ShouldBe(0);
        report.DistinctTargets.ShouldBe(2);
        report.ScoreCard.Score.ShouldBe(67);
        report.ScoreCard.Accent.ShouldBe("amber");
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

        parser.SearingBlazeSpreadReports.ShouldBeEmpty();
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

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(2);
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

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(1);
        report.TotalApplications.ShouldBe(1);
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

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(2);
        report.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_LowCoverage_MajorFindingEmberAccent()
    {
        var events = new List<Event>
        {
            Apply(targetId: 10, targetInstance: 0, timestamp: 1000),
        };

        var (parser, _) = await AnalyzeAsync(events, TrashFight(10));

        var report = parser.SearingBlazeSpreadReports.ShouldHaveSingleItem().Result;
        report.DistinctTargets.ShouldBe(1);
        report.ScoreCard.Score.ShouldBe(10);
        report.ScoreCard.Accent.ShouldBe("ember");
        report.Findings[0].Severity.ShouldBe("major");
    }

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
        new(0, "", 0, null, 0, 20000, null, null, null, EnemyNpcs: [new FightNpc(1, 100, enemies, null, null)]);

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
