using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;
using FellowshipAnalyzer.Heroes.Gunde.Statistics;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Items = FellowshipAnalyzer.Core.Common.Items.Items;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class FatedStrikeWindowTrackerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int PullEnd = 200_000;

    [Fact]
    public async Task Analyze_PriorityAndFillerInsideAWindow_CountsOnlyTheInWindowCasts()
    {
        var events = new List<Event>
        {
            Cast(Items.FatedStrike.FSLID, 9_900),
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 10_500),
            Cast(Spells.DoubleStrike.FSLID, 11_500),
            Cast(Spells.HeartSplitter.FSLID, 12_500),
            WindowRemoved(16_000),
            Cast(Spells.BloodArc.FSLID, 20_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(1);
        tracker.PriorityCasts.ShouldBe(2);
        tracker.FillerCasts.ShouldBe(1);
        tracker.ClassifiedCasts.ShouldBe(3);
        tracker.PriorityShare.ShouldBe(2d / 3d, 0.0001);
    }

    [Fact]
    public async Task Analyze_RefreshMidWindow_ExtendsTheWindowRatherThanOpeningANewOne()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 10_500),
            WindowRefreshed(12_000),
            Cast(Spells.BloodArc.FSLID, 13_000),
            WindowRemoved(18_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(1);
        tracker.PriorityCasts.ShouldBe(2);
        tracker.FillerCasts.ShouldBe(0);
        tracker.PriorityShare.ShouldBe(1d);
    }

    [Fact]
    public async Task Analyze_TwoSeparateWindows_CountsBoth()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 10_500),
            WindowRemoved(16_000),
            WindowApplied(80_000),
            Cast(Spells.ReaverEdge.FSLID, 80_500),
            WindowRemoved(86_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(2);
        tracker.PriorityCasts.ShouldBe(1);
        tracker.FillerCasts.ShouldBe(1);
        tracker.PriorityShare.ShouldBe(0.5);
    }

    [Fact]
    public async Task Analyze_SecondApplicationWithNoRemoval_CountsASecondActivation()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 10_500),
            WindowApplied(70_000),
            Cast(Spells.BloodArc.FSLID, 70_500),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(2);
        tracker.PriorityCasts.ShouldBe(2);
        tracker.FillerCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WindowWithNoRemoval_StopsClassifyingWhenTheBuffWouldHaveExpired()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 12_000),
            Cast(Spells.BloodArc.FSLID, 20_000),
            Cast(Spells.DoubleStrike.FSLID, 40_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(1);
        tracker.PriorityCasts.ShouldBe(1);
        tracker.FillerCasts.ShouldBe(0);
        tracker.ClassifiedCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_RefreshAfterAnExpiredWindow_OpensANewWindow()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            WindowRefreshed(30_000),
            Cast(Spells.GrimCarve.FSLID, 30_500),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(2);
        tracker.PriorityCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_UnclassifiedAbilityInsideAWindow_ChangesNothing()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.Slaughter.FSLID, 10_500),
            Cast(Spells.Rupture.FSLID, 11_500),
            WindowRemoved(16_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(1);
        tracker.PriorityCasts.ShouldBe(0);
        tracker.FillerCasts.ShouldBe(0);
        tracker.ClassifiedCasts.ShouldBe(0);
        tracker.PriorityShare.ShouldBe(0d);
    }

    [Fact]
    public async Task Analyze_CastsOutsideEveryWindow_AreNotClassified()
    {
        var events = new List<Event>
        {
            Cast(Spells.GrimCarve.FSLID, 5_000),
            Cast(Spells.DoubleStrike.FSLID, 6_000),
            WindowApplied(10_000),
            WindowRemoved(16_000),
            Cast(Spells.BloodArc.FSLID, 20_000),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Windows.ShouldBe(1);
        tracker.ClassifiedCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_NoWindowAndNoActivation_ContributesNoStatisticsCard()
    {
        var (parser, result) = await RunAsync([Cast(Spells.Slaughter.FSLID, 5_000)], BossFight(PullEnd));

        var tracker = parser.FatedStrikeWindowTracker.ShouldNotBeNull();
        tracker.Windows.ShouldBe(0);
        tracker.FatedStrikeCasts.ShouldBe(0);
        tracker.StatisticsComponentType.ShouldBeNull();
        result.Statistics.ShouldNotContain(entry => entry.ComponentType == typeof(FatedStrikeStatistics));
    }

    [Fact]
    public async Task Analyze_WithAWindow_ContributesTheStatisticsCard()
    {
        var events = new List<Event>
        {
            WindowApplied(10_000),
            Cast(Spells.GrimCarve.FSLID, 10_500),
            WindowRemoved(16_000),
        };

        var (parser, result) = await RunAsync(events, BossFight(PullEnd));

        var tracker = parser.FatedStrikeWindowTracker.ShouldNotBeNull();
        tracker.StatisticsComponentType.ShouldBe(typeof(FatedStrikeStatistics));
        tracker.StatisticCategory.ShouldBe(Core.UI.StatisticCategory.Items);
        result.Statistics.ShouldContain(entry => entry.ComponentType == typeof(FatedStrikeStatistics));
    }

    [Fact]
    public async Task Analyze_ActivationWithNoBuffEvents_StillEvidencesTheWeapon()
    {
        var (parser, result) = await RunAsync([Cast(Items.FatedStrike.FSLID, 5_000)], BossFight(PullEnd));

        var tracker = parser.FatedStrikeWindowTracker.ShouldNotBeNull();
        tracker.FatedStrikeCasts.ShouldBe(1);
        tracker.Windows.ShouldBe(0);
        tracker.StatisticsComponentType.ShouldBe(typeof(FatedStrikeStatistics));
        result.Statistics.ShouldContain(entry => entry.ComponentType == typeof(FatedStrikeStatistics));
    }

    [Fact]
    public async Task Analyze_ClassifiedCastsAlwaysBelongToACountedWindow()
    {
        var events = new List<Event>
        {
            WindowRefreshed(10_000),
            Cast(Spells.HeartSplitter.FSLID, 10_500),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.ClassifiedCasts.ShouldBe(1);
        tracker.Windows.ShouldBe(1);
    }

    private static ApplyBuffEvent WindowApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Items.GloriousPurpose.FSLID },
    };

    private static RefreshBuffEvent WindowRefreshed(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Items.GloriousPurpose.FSLID },
    };

    private static RemoveBuffEvent WindowRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Items.GloriousPurpose.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static ReportFight BossFight(int endTime) =>
        new(0, "Boss", 1, null, 0, endTime, null, null, null);

    private static async Task<FatedStrikeWindowTracker> AnalyzeAsync(List<Event> events)
    {
        var (parser, _) = await RunAsync(events, BossFight(PullEnd));
        return parser.FatedStrikeWindowTracker.ShouldNotBeNull();
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
