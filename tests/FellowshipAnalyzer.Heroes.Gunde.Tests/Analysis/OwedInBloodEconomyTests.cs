using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;
using FellowshipAnalyzer.Heroes.Gunde.Statistics;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class OwedInBloodEconomyTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int PullEnd = 200_000;

    [Fact]
    public async Task Analyze_BankBuiltThenCashedIn_RecordsTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 10),
            StackApplied(3_000, 40),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.Timestamp.ShouldBe(10_000);
        conversion.StacksConverted.ShouldBe(40);

        analyzer.TotalStacksConverted.ShouldBe(40);
        analyzer.BestConversion.ShouldBe(40);
        analyzer.AverageConversion.ShouldBe(40d);
        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.CappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ApplyWithoutAStackEvent_CountsASingleFeather()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
            BuffRemoved(5_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(1);
        analyzer.TotalStacksConverted.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_TwoConversionsOfDifferentSizes_ReportsBestAndAverage()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 20),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            BuffApplied(20_000),
            StackApplied(25_000, 60),
            Cast(Spells.OwedInBlood.FSLID, 30_000),
            BuffRemoved(30_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.Count.ShouldBe(2);
        analyzer.Conversions[0].StacksConverted.ShouldBe(20);
        analyzer.Conversions[1].StacksConverted.ShouldBe(60);

        analyzer.TotalStacksConverted.ShouldBe(80);
        analyzer.BestConversion.ShouldBe(60);
        analyzer.AverageConversion.ShouldBe(40d);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BuffFallingOffWithNoCast_CountsEveryHeldStackAsDecayed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(50_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(25);
        analyzer.Conversions.ShouldBeEmpty();
        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.BestConversion.ShouldBe(0);
        analyzer.AverageConversion.ShouldBe(0d);
    }

    [Fact]
    public async Task Analyze_BuffRemovedRightAfterACast_IsTheConversionNotDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_500),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_BuffRemovedLongAfterACast_IsDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            StackApplied(11_000, 25),
            BuffRemoved(40_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(25);
        analyzer.DecayedStacks.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_PartialStackLossWithNoCast_CountsOnlyTheStacksLost()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            StackRemoved(30_000, 10),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(15);
        analyzer.Conversions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_BankPinnedAtTheCap_MeasuresTheCappedSpanFromTheFirstArrival()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(20_000, OwedInBloodEconomyAnalyzer.MaxStacks),
            StackApplied(25_000, OwedInBloodEconomyAnalyzer.MaxStacks),
            Cast(Spells.OwedInBlood.FSLID, 40_000),
            BuffRemoved(40_200),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.CappedMs.ShouldBe(20_200);
        analyzer.TotalStacksConverted.ShouldBe(OwedInBloodEconomyAnalyzer.MaxStacks);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_StillCappedWhenThePullEnds_ClosesTheSpanAtThePullBoundary()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(150_000, OwedInBloodEconomyAnalyzer.MaxStacks),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.CappedMs.ShouldBe(PullEnd - 150_000);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_PullEndingWhileHoldingStacks_CountsThemAsNeitherConvertedNorDecayed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 30),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldBeEmpty();
        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.CappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastWithNoBankedStacks_RecordsAnEmptyConversion()
    {
        var analyzer = await AnalyzeAsync([Cast(Spells.OwedInBlood.FSLID, 10_000)]);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(0);
        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.BestConversion.ShouldBe(0);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrashPull_IsAlsoEvaluated()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 12),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
        };

        var (parser, _) = await RunAsync(events, TrashFight(PullEnd));

        parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem()
            .Analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(12);
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([BuffApplied(1_000)], BossFight(PullEnd));

        var entry = parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.OwedInBloodEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).OwedInBloodEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_WithBloodFeatherResourceData_ContributesTheStatisticsCard()
    {
        var (parser, result) = await RunAsync([CastWithFeathers(1_000, rawAmount: 4_200)], BossFight(PullEnd));

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.StatisticsComponentType.ShouldBe(typeof(BloodFeatherStatistics));
        result.Statistics.ShouldContain(entry => entry.ComponentType == typeof(BloodFeatherStatistics));
    }

    [Fact]
    public async Task Analyze_WithNoBloodFeatherResourceData_ContributesNoStatisticsCard()
    {
        var (parser, result) = await RunAsync([Cast(Spells.HeartSplitter.FSLID, 1_000)], BossFight(PullEnd));

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.BloodFeathers.ShouldBeNull();
        tracker.StatisticsComponentType.ShouldBeNull();
        result.Statistics.ShouldNotContain(entry => entry.ComponentType == typeof(BloodFeatherStatistics));
    }

    private static ApplyBuffEvent BuffApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static ApplyBuffStackEvent StackApplied(int timestamp, int stacks) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stacks,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static RemoveBuffStackEvent StackRemoved(int timestamp, int stacks) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stacks,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static RemoveBuffEvent BuffRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static CastEvent CastWithFeathers(int timestamp, int rawAmount)
    {
        var cast = Cast(Spells.HeartSplitter.FSLID, timestamp);
        cast.SourceResources = new ActorResources
        {
            HitPoints = 1_000,
            MaxHitPoints = 1_000,
            Resources =
            [
                new ClassResource { Type = ResourceTypes.Tertiary, Amount = rawAmount, Max = 15_000 },
            ],
        };
        return cast;
    }

    private static ReportFight BossFight(int endTime) =>
        new(0, "Boss", 1, null, 0, endTime, null, null, null);

    private static ReportFight TrashFight(int endTime) =>
        new(0, "Trash", 0, null, 0, endTime, null, null, null, EnemyNpcs: [new FightNpc(1, 100, 4, null, null)]);

    private static async Task<OwedInBloodEconomyAnalyzer> AnalyzeAsync(List<Event> events, ReportFight? fight = null)
    {
        var (parser, _) = await RunAsync(events, fight ?? BossFight(PullEnd));
        return parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> RunAsync(
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

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
