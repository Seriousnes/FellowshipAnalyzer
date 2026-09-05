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

public sealed class TemporalBarrageAnalyzerTests
{
    private const int PlayerId = 7;
    private const int TankId = 90;
    private const int EnemyId = 82;
    private const int DungeonEndTime = 60_000;
    private const long TankMaxHitPoints = 36_268;

    [Fact]
    public async Task Channels_TakeTheBoltsBetweenOneChannelStartAndTheNext()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageDamage(1_500, 600),
            BeginChannel(5_000),
            BarrageDamage(5_200, 700));

        analyzer.Channels.Count.ShouldBe(2);
        analyzer.Channels[0].Ticks.ShouldBe(2);
        analyzer.Channels[0].Damage.ShouldBe(1_100);
        analyzer.Channels[1].Ticks.ShouldBe(1);
        analyzer.Channels[1].Damage.ShouldBe(700);
        analyzer.TotalDamage.ShouldBe(1_800);
        analyzer.TotalTicks.ShouldBe(3);
    }

    [Fact]
    public async Task Bolts_CountOnceWhenDamageAndHealShareTheInstant()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageHeal(1_200, TankId, 500),
            BarrageDamage(1_500, 600),
            BarrageHeal(1_500, TankId, 600));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Ticks.ShouldBe(2);
        channel.TickTimestamps.ShouldBe([1_200, 1_500]);
    }

    [Fact]
    public async Task ChannelEnd_FallsBackToTheLastBoltWhenTheLogReportsNone()
    {
        var analyzer = await Track(BeginChannel(1_000), BarrageDamage(1_200, 500), BarrageDamage(1_500, 600));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.End.ShouldBe(1_500);
        channel.EndReported.ShouldBeFalse();
        channel.DurationMs.ShouldBe(500);
    }

    [Fact]
    public async Task ChannelEnd_UsesTheReportedEndWhenTheLogCarriesOne()
    {
        var analyzer = await Track(BeginChannel(1_000), BarrageDamage(1_200, 500), EndChannel(3_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.End.ShouldBe(3_000);
        channel.EndReported.ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelEnd_KeepsTheLastBoltWhenItLandsAfterTheReportedEnd()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            EndChannel(2_000),
            BarrageDamage(2_400, 600));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.End.ShouldBe(2_400);
        channel.EndReported.ShouldBeTrue();
        channel.Ticks.ShouldBe(2);
        channel.Damage.ShouldBe(1_100);
    }

    [Fact]
    public async Task ChannelEnd_FallsBackToTheStartWhenTheChannelLandedNothing()
    {
        var analyzer = await Track(BeginChannel(1_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.End.ShouldBe(1_000);
        channel.DurationMs.ShouldBe(0);
    }

    [Fact]
    public async Task Target_ReadsEnemyFromTheBoltsThatDamaged()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageHeal(1_200, TankId, 500));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Target.ShouldBe(BarrageTarget.Enemy);
        channel.DamageTargets.ShouldBe([EnemyId]);
        channel.HealTargets.ShouldBe([TankId]);
        analyzer.EnemyChannels.ShouldBe(1);
        analyzer.AllyChannels.ShouldBe(0);
    }

    [Fact]
    public async Task Target_ReadsAllyWhenNoBoltDamaged()
    {
        var analyzer = await Track(BeginChannel(1_000), BarrageHeal(1_200, TankId, 500), BarrageHeal(1_500, TankId, 600));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Target.ShouldBe(BarrageTarget.Ally);
        channel.PrimaryHealTargetId.ShouldBe(TankId);
        analyzer.AllyChannels.ShouldBe(1);
    }

    [Fact]
    public async Task Target_IsUnknownWhenTheChannelLandedNoBolt()
    {
        var analyzer = await Track(BeginChannel(1_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Ticks.ShouldBe(0);
        channel.Target.ShouldBe(BarrageTarget.Unknown);
        channel.PrimaryHealTargetId.ShouldBeNull();
        analyzer.ChannelsWithoutBolts.ShouldBe(1);
    }

    [Fact]
    public async Task Healing_SplitsWhatLandedFromWhatOverhealed()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageHeal(1_200, TankId, 400, overheal: 100),
            BarrageHeal(1_500, TankId, 400, overheal: 100));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.HealEffective.ShouldBe(800);
        channel.Overheal.ShouldBe(200);
        channel.HealTotal.ShouldBe(1_000);
        channel.HealEfficiency.ShouldBe(0.8, 0.0001);
        analyzer.TotalHealEffective.ShouldBe(800);
        analyzer.TotalOverheal.ShouldBe(200);
    }

    [Fact]
    public async Task StaggerCleared_ReconstructsTheAllyPoolAcrossAnAllyChannel()
    {
        var analyzer = await Track(
            StaggerReading(900, TankId, rawStagger: 500_000),
            BeginChannel(1_000),
            BarrageHeal(1_200, TankId, 400, rawStagger: 400_000),
            BarrageHeal(1_500, TankId, 400, rawStagger: 300_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Target.ShouldBe(BarrageTarget.Ally);
        channel.StaggerPreTimestamp.ShouldBe(900);
        channel.StaggerPostTimestamp.ShouldBe(1_500);
        channel.StaggerCleared.ShouldBe(2_000);
        analyzer.StaggerCleared.ShouldBe(2_000);
        analyzer.MeasuredStaggerChannels.ShouldBe(1);
    }

    [Fact]
    public async Task StaggerCleared_IsAbsentWithoutAReadingBeforeTheChannel()
    {
        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageHeal(1_200, TankId, 400, rawStagger: 400_000),
            BarrageHeal(1_500, TankId, 400, rawStagger: 300_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.StaggerCleared.ShouldBeNull();
        analyzer.MeasuredStaggerChannels.ShouldBe(0);
    }

    [Fact]
    public async Task StaggerCleared_IsAbsentOnAnEnemyChannel()
    {
        var analyzer = await Track(
            StaggerReading(900, TankId, rawStagger: 500_000),
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageHeal(1_200, TankId, 400, rawStagger: 300_000));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.Target.ShouldBe(BarrageTarget.Enemy);
        channel.StaggerCleared.ShouldBeNull();
    }

    [Fact]
    public async Task TemporalShift_NotTaken_LeavesTheFleetingHourBenefitAtZero()
    {
        var analyzer = await Track(
            FleetingHourApply(900),
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageDamage(1_500, 600));

        analyzer.TemporalShiftTaken.ShouldBeFalse();
        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.TicksWhileFleetingHourActive.ShouldBe(0);
        channel.FleetingHourDurationExtensionMs.ShouldBe(0);
        channel.FleetingHourCooldownReduction.Total.ShouldBe(0);
        channel.FleetingHourActiveAtStart.ShouldBeTrue();
        analyzer.ChannelsUnderFleetingHour.ShouldBe(1);
    }

    [Fact]
    public async Task TemporalShift_AddsDurationForTheBoltsInsideTheWindow()
    {
        var analyzer = await Track(
            [AeonaTalents.TemporalShift],
            FleetingHourApply(900),
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageDamage(1_500, 600),
            FleetingHourRemove(5_000));

        analyzer.TemporalShiftTaken.ShouldBeTrue();
        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.TicksWhileFleetingHourActive.ShouldBe(2);
        channel.TicksWhileFleetingHourInactive.ShouldBe(0);
        channel.FleetingHourDurationExtensionMs.ShouldBe(600);
        channel.FleetingHourCooldownReduction.Total.ShouldBe(0);
        analyzer.FleetingHourDurationExtensionMs.ShouldBe(600);
    }

    [Fact]
    public async Task TemporalShift_ShortensTheFleetingHourCooldownForTheBoltsOutsideTheWindow()
    {
        var analyzer = await Track(
            [AeonaTalents.TemporalShift],
            FleetingHourCast(500),
            FleetingHourApply(500),
            FleetingHourRemove(900),
            BeginChannel(1_000),
            BarrageDamage(1_200, 500),
            BarrageDamage(1_500, 600));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.TicksWhileFleetingHourActive.ShouldBe(0);
        channel.TicksWhileFleetingHourInactive.ShouldBe(2);
        channel.FleetingHourDurationExtensionMs.ShouldBe(0);
        channel.FleetingHourCooldownReduction.Total.ShouldBe(600);
        channel.FleetingHourCooldownReduction.Effective.ShouldBe(600);
        channel.FleetingHourCooldownReduction.Wasted.ShouldBe(0);
        analyzer.FleetingHourCooldownReduction.Effective.ShouldBe(600);
    }

    [Fact]
    public async Task TemporalShift_WastesTheReductionWhenFleetingHourIsAlreadyAvailable()
    {
        var analyzer = await Track(
            [AeonaTalents.TemporalShift],
            BeginChannel(1_000),
            BarrageDamage(1_200, 500));

        var channel = analyzer.Channels.ShouldHaveSingleItem();
        channel.FleetingHourCooldownReduction.Total.ShouldBe(300);
        channel.FleetingHourCooldownReduction.Effective.ShouldBe(0);
        channel.FleetingHourCooldownReduction.Wasted.ShouldBe(300);
    }

    [Fact]
    public async Task CompletedChannels_CountTheChannelsThatLandedEveryBolt()
    {
        TemporalBarrageAnalyzer.ExpectedBolts.ShouldBe(4);

        var analyzer = await Track(
            BeginChannel(1_000),
            BarrageDamage(1_100, 100),
            BarrageDamage(1_200, 100),
            BarrageDamage(1_300, 100),
            BarrageDamage(1_400, 100),
            BeginChannel(5_000),
            BarrageDamage(5_100, 100),
            BarrageDamage(5_200, 100));

        analyzer.Channels.Count.ShouldBe(2);
        analyzer.CompletedChannels.ShouldBe(1);
    }

    [Fact]
    public async Task ParadoxicalTwist_IsReportedFromTheBuild()
    {
        var withTalent = await Track([AeonaTalents.ParadoxicalTwist], BeginChannel(1_000), BarrageDamage(1_200, 500));
        var withoutTalent = await Track(BeginChannel(1_000), BarrageDamage(1_200, 500));

        withTalent.ParadoxicalTwistTaken.ShouldBeTrue();
        withoutTalent.ParadoxicalTwistTaken.ShouldBeFalse();
    }

    [Fact]
    public async Task Channels_AreReachableThroughThePullReadPaths()
    {
        var parser = await AnalyzeAsync([Talented([]), BeginChannel(1_000), BarrageDamage(1_200, 500)]);

        var entry = parser.TemporalBarrageAnalyzers.ShouldHaveSingleItem();
        entry.Analyzer.Channels.ShouldHaveSingleItem().Damage.ShouldBe(500);
        entry.Pull.TemporalBarrageAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).TemporalBarrageAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static BeginChannelEvent BeginChannel(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.TemporalBarrage.FSLID },
    };

    private static EndChannelEvent EndChannel(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Start = 1_000,
        Duration = timestamp - 1_000,
        Ability = new Ability { Id = Spells.TemporalBarrage.FSLID },
    };

    private static DamageEvent BarrageDamage(int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Amount = amount,
        Tick = true,
        Ability = new Ability { Id = Spells.TemporalBarrage.FSLID },
    };

    private static HealEvent BarrageHeal(int timestamp, int targetId, long amount, long overheal = 0, int? rawStagger = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = amount,
        Overheal = overheal,
        Ability = new Ability { Id = Spells.TemporalBarrage.FSLID },
        TargetResources = rawStagger is { } stagger ? StaggerResources(stagger) : null,
    };

    private static HealEvent StaggerReading(int timestamp, int targetId, int rawStagger) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = 100,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = StaggerResources(rawStagger),
    };

    private static ActorResources StaggerResources(int rawStagger) => new()
    {
        HitPoints = TankMaxHitPoints / 2,
        MaxHitPoints = TankMaxHitPoints,
        Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = rawStagger, Max = -100 }],
    };

    private static ApplyBuffEvent FleetingHourApply(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.FleetingHourSelfBuff.FSLID },
    };

    private static RemoveBuffEvent FleetingHourRemove(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.FleetingHourSelfBuff.FSLID },
    };

    private static CastEvent FleetingHourCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.FleetingHour.FSLID },
    };

    private static CombatantInfoEvent Talented(int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
    };

    private static Task<TemporalBarrageAnalyzer> Track(params Event[] events) => Track([], events);

    private static async Task<TemporalBarrageAnalyzer> Track(int[] talents, params Event[] events)
    {
        var parser = await AnalyzeAsync([Talented(talents), .. events]);
        return parser.TemporalBarrageAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "Boss", 1, true, 0, DungeonEndTime, null, null, null));
        return parser;
    }
}
