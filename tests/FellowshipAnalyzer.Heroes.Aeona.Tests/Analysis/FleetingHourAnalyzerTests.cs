using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class FleetingHourAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEndTime = 60_000;

    [Fact]
    public async Task ApplyThenRemove_OpensAndClosesOneWindow()
    {
        var analyzer = await Track(SinglePullDungeon(), Apply(1_000), Remove(14_000));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(14_000);
        analyzer.TotalUptimeMs.ShouldBe(13_000);
    }

    [Fact]
    public async Task ApplyInsideAnOpenWindow_DoesNotOpenASecond()
    {
        var analyzer = await Track(
            SinglePullDungeon(),
            Apply(1_000),
            Apply(2_000),
            Refresh(3_000),
            Remove(14_000));

        analyzer.Windows.ShouldHaveSingleItem().Duration.ShouldBe(13_000);
    }

    [Fact]
    public async Task WindowStillOpenAtTheDungeonEnd_ClosesAtTheDungeonEnd()
    {
        var analyzer = await Track(SinglePullDungeon(), Apply(1_000), Remove(14_000), Apply(50_000));

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[1].End.ShouldBe(DungeonEndTime);
        analyzer.IsBuffActiveAt(DungeonEndTime).ShouldBeTrue();
    }

    [Fact]
    public async Task IsBuffActiveAt_IncludesTheEndpoints()
    {
        var analyzer = await Track(SinglePullDungeon(), Apply(1_000), Remove(14_000));

        analyzer.IsBuffActiveAt(1_000).ShouldBeTrue();
        analyzer.IsBuffActiveAt(14_000).ShouldBeTrue();
        analyzer.IsBuffActiveAt(999).ShouldBeFalse();
        analyzer.IsBuffActiveAt(14_001).ShouldBeFalse();
    }

    [Fact]
    public async Task CombatUptime_CountsOnlyTheWindowTimeInsideAPull()
    {
        var analyzer = await Track(PullsDungeon((5_000, 15_000)), Apply(3_000), Remove(10_000));

        analyzer.TotalUptimeMs.ShouldBe(7_000);
        analyzer.CombatUptimeMs.ShouldBe(5_000);
        analyzer.OutsideCombatMs.ShouldBe(2_000);
        analyzer.CombatUptime.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task OutsideCombatMs_CountsTheWindowTimeBetweenTwoPulls()
    {
        var analyzer = await Track(
            PullsDungeon((0, 5_000), (10_000, 20_000)),
            Apply(3_000),
            Remove(13_000));

        analyzer.TotalUptimeMs.ShouldBe(10_000);
        analyzer.OutsideCombatMs.ShouldBe(5_000);
        analyzer.OutsideCombatShare.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task UptimeMsIn_AndCastsIn_ReadOnePull()
    {
        var dungeon = PullsDungeon((0, 10_000), (20_000, 30_000));
        var parser = await AnalyzeAsync(dungeon, Cast(1_000), Apply(1_000), Remove(6_000), Cast(21_000), Apply(21_000), Remove(25_000));
        var analyzer = parser.FleetingHour.ShouldNotBeNull();

        analyzer.UptimeMsIn(parser.Pulls[0]).ShouldBe(5_000);
        analyzer.UptimeIn(parser.Pulls[0]).ShouldBe(0.5, 0.0001);
        analyzer.CastsIn(parser.Pulls[0]).ShouldHaveSingleItem().Timestamp.ShouldBe(1_000);
        analyzer.UptimeMsIn(parser.Pulls[1]).ShouldBe(4_000);
        analyzer.CastsIn(parser.Pulls[1]).ShouldHaveSingleItem().Timestamp.ShouldBe(21_000);
    }

    [Fact]
    public async Task ACastsWindow_SplitsIntoTheTimeInsideThePullAndTheTimeAfterIt()
    {
        var analyzer = await Track(
            PullsDungeon((0, 10_000)),
            Cast(5_000),
            Apply(5_000),
            Remove(20_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Window.ShouldNotBeNull().Duration.ShouldBe(15_000);
        cast.ActiveInPullMs.ShouldBe(5_000);
        cast.OutsideCombatMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task TheCooldownIsHeldUntilTheWindowEnds()
    {
        var analyzer = await Track(
            PullsDungeon((0, DungeonEndTime)),
            Cast(1_000),
            Apply(1_000),
            Remove(14_000));

        analyzer.AvailableInCombatMs.ShouldBe(1_000 + (DungeonEndTime - 34_000));
    }

    [Fact]
    public async Task DelayAfterAvailable_CountsCombatTimeAlone()
    {
        var analyzer = await Track(
            PullsDungeon((0, 10_000), (30_000, 50_000)),
            Cast(35_000),
            Apply(35_000),
            Remove(40_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.DelayMs.ShouldBe(15_000);
        analyzer.AverageDelayMs.ShouldBe(15_000d, 0.0001);
    }

    [Fact]
    public void SurgingChronaGrant_ComesFromTheTalentRecord() =>
        Talents.SurgingChrona.ResourceGeneration.ShouldNotBeNull().Amount.ShouldBe(30);

    [Fact]
    public async Task SurgingChrona_WithoutTheTalent_GrantsNothing()
    {
        var analyzer = await Track(SinglePullDungeon(), Cast(1_000, rawChrona: 8_000));

        analyzer.SurgingChronaTaken.ShouldBeFalse();
        analyzer.SurgingChronaGrant.ShouldBe(0);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.SurgingChronaGranted.ShouldBe(0);
        cast.SurgingChronaOvercapped.ShouldBe(0);
        analyzer.SurgingChronaOvercapped.ShouldBe(0);
    }

    [Fact]
    public async Task SurgingChrona_OvercapsWhatTheCapRefused()
    {
        var analyzer = await Track(
            SinglePullDungeon(),
            [AeonaTalents.SurgingChrona],
            Cast(1_000, rawChrona: 8_000),
            Cast(30_000, rawChrona: 2_000));

        analyzer.SurgingChronaTaken.ShouldBeTrue();
        analyzer.SurgingChronaGrant.ShouldBe(30);
        analyzer.Casts[0].SurgingChronaOvercapped.ShouldBe(10);
        analyzer.Casts[1].SurgingChronaOvercapped.ShouldBe(0);
        analyzer.SurgingChronaGranted.ShouldBe(60);
        analyzer.SurgingChronaOvercapped.ShouldBe(10);
        analyzer.SurgingChronaOvercapShare.ShouldBe(10 / 60d, 0.0001);
    }

    [Fact]
    public async Task SurgingChrona_WithNoChronaAtTheCast_LeavesTheOvercapNull()
    {
        var analyzer = await Track(
            SinglePullDungeon(),
            [AeonaTalents.SurgingChrona],
            Cast(1_000),
            Cast(30_000, rawChrona: 9_500));

        analyzer.Casts[0].SurgingChronaOvercapped.ShouldBeNull();
        analyzer.Casts[1].SurgingChronaOvercapped.ShouldBe(25);
        analyzer.SurgingChronaOvercapped.ShouldBe(25);
    }

    private static ApplyBuffEvent Apply(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.FleetingHourSelfBuff.FSLID },
    };

    private static RefreshBuffEvent Refresh(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.FleetingHourSelfBuff.FSLID },
    };

    private static RemoveBuffEvent Remove(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.FleetingHourSelfBuff.FSLID },
    };

    private static CastEvent Cast(int timestamp, int? rawChrona = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.FleetingHour.FSLID },
        SourceResources = new ActorResources
        {
            Resources = rawChrona is { } amount
                ? [new ClassResource { Type = ResourceTypes.Primary, Amount = amount, Max = 10_000 }]
                : [],
        },
    };

    private static CombatantInfoEvent Talented(int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
    };

    private static ReportDungeon SinglePullDungeon() =>
        new(0, "Boss", 1, true, 0, DungeonEndTime, null, null, null);

    private static ReportDungeon PullsDungeon(params (int Start, int End)[] pulls) =>
        new(0, "Dungeon", 1, true, 0, DungeonEndTime, null, null, null, false,
            [.. pulls.Select((pull, index) =>
                new DungeonPull(index + 1, 1, true, pull.Start, pull.End, $"Pull {index + 1}", null))]);

    private static Task<FleetingHourAnalyzer> Track(ReportDungeon dungeon, params Event[] events) =>
        Track(dungeon, [], events);

    private static async Task<FleetingHourAnalyzer> Track(ReportDungeon dungeon, int[] talents, params Event[] events)
    {
        var parser = await AnalyzeAsync(dungeon, [Talented(talents), .. events]);
        return parser.FleetingHour.ShouldNotBeNull();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(ReportDungeon dungeon, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, dungeon);
        return parser;
    }
}
