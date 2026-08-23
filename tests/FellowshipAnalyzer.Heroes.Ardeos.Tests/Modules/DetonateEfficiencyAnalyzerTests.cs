using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Core;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class DetonateEfficiencyAnalyzerTests
{
    private const int PlayerId = 7;
    private const int Enemy1 = 100;
    private const int Enemy2 = 200;
    private const int Instance = 1;
    private const int ApocalypticSurgeTalentId = 678;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Ardeos", "Player", null, null, null),
        new(Enemy1, "Enemy 1", "NPC", null, null, null),
        new(Enemy2, "Enemy 2", "NPC", null, null, null),
    ];

    [Fact]
    public async Task SingleTargetWithFourDistinctDoTs_IsWellLayeredAtFour()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireBallDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireFrogsDot.FSLID),
            Detonate(1000));

        analyzer.TotalCasts.ShouldBe(1);
        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetsWithDoTs.ShouldBe(1);
        cast.TotalInstances.ShouldBe(4);
        cast.AverageInstances.ShouldBe(4.0);
        cast.MaxTargetInstances.ShouldBe(4);
        analyzer.WellLayeredCasts.ShouldBe(1);
        analyzer.UnderLayeredCasts.ShouldBe(0);
        analyzer.AverageInstancesPerTarget.ShouldBe(4.0);
        analyzer.MaxInstances.ShouldBe(4);
    }

    [Fact]
    public async Task ConcurrentEngulfingFlames_CountEachApplicationAsOneInstance()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(150, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetsWithDoTs.ShouldBe(1);
        cast.TotalInstances.ShouldBe(2);
        cast.AverageInstances.ShouldBe(2.0);
    }

    [Fact]
    public async Task StackedIncinerate_CountsAsOneInstanceRegardlessOfStacks()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.IncinerateDot.FSLID),
            ApplyDebuffStack(150, Enemy1, Spells.IncinerateDot.FSLID, stack: 5),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetsWithDoTs.ShouldBe(1);
        cast.TotalInstances.ShouldBe(1);
        cast.AverageInstances.ShouldBe(1.0);
        analyzer.UnderLayeredCasts.ShouldBe(1);
    }

    [Fact]
    public async Task MultiTarget_AveragesInstancesAcrossEnemies()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireBallDot.FSLID),
            ApplyDebuff(100, Enemy2, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy2, Spells.FireBallDot.FSLID),
            ApplyDebuff(100, Enemy2, Spells.FireFrogsDot.FSLID),
            ApplyDebuff(100, Enemy2, Spells.EngulfingFlamesDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetsWithDoTs.ShouldBe(2);
        cast.TotalInstances.ShouldBe(6);
        cast.AverageInstances.ShouldBe(3.0);
        cast.MaxTargetInstances.ShouldBe(4);
        analyzer.WellLayeredCasts.ShouldBe(1);
    }

    [Fact]
    public async Task DeadEnemyDoTs_AreExcludedFromLaterCasts()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireBallDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireFrogsDot.FSLID),
            Death(500, Enemy1),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetsWithDoTs.ShouldBe(0);
        cast.TotalInstances.ShouldBe(0);
        analyzer.UnderLayeredCasts.ShouldBe(1);
    }

    [Fact]
    public async Task CastWithNoDoTCarryingEnemies_BucketsIntoDistributionAtZero()
    {
        var analyzer = await Analyze(
            Combatant(),
            Detonate(1000));

        analyzer.TotalCasts.ShouldBe(1);
        analyzer.Casts.ShouldHaveSingleItem().TargetsWithDoTs.ShouldBe(0);
        analyzer.InstanceDistribution.ShouldContainKeyAndValue(0, 1);
    }

    [Fact]
    public async Task Distribution_BucketsEachCastByRoundedAverage()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireBallDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.FireFrogsDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            Detonate(1000),
            Death(1500, Enemy1),
            Detonate(2000));

        analyzer.TotalCasts.ShouldBe(2);
        analyzer.InstanceDistribution.ShouldContainKeyAndValue(4, 1);
        analyzer.InstanceDistribution.ShouldContainKeyAndValue(0, 1);
    }

    [Fact]
    public async Task ApocalypticSurge_TwoFreeDetonates_AreMarkedFree()
    {
        var analyzer = await Analyze(
            Combatant(talented: true),
            ApplyBuff(0, Spells.ApocalypticSurge.FSLID),
            ApplyBuffStack(0, Spells.ApocalypticSurge.FSLID, stack: 2),
            Detonate(1000),
            RemoveBuffStack(1001, Spells.ApocalypticSurge.FSLID, stack: 1),
            Detonate(2000),
            RemoveBuff(2001, Spells.ApocalypticSurge.FSLID));

        analyzer.ApocalypticSurgeTalented.ShouldBeTrue();
        analyzer.TotalCasts.ShouldBe(2);
        analyzer.FreeCasts.ShouldBe(2);
        analyzer.PaidCasts.ShouldBe(0);
        analyzer.SurgeStacksGained.ShouldBe(2);
        analyzer.SurgeStacksWasted.ShouldBe(0);
    }

    [Fact]
    public async Task ApocalypticSurge_ExpiryWithNoCastUnderBuff_IsWasted()
    {
        var analyzer = await Analyze(
            Combatant(talented: true),
            ApplyBuff(0, Spells.ApocalypticSurge.FSLID),
            ApplyBuffStack(0, Spells.ApocalypticSurge.FSLID, stack: 2),
            RemoveBuff(5000, Spells.ApocalypticSurge.FSLID),
            Detonate(6000));

        analyzer.FreeCasts.ShouldBe(0);
        analyzer.PaidCasts.ShouldBe(1);
        analyzer.SurgeStacksGained.ShouldBe(2);
        analyzer.SurgeStacksWasted.ShouldBe(2);
    }

    [Fact]
    public async Task ApocalypticSurge_FinalChargeRemovalLoggedBeforeCast_IsStillFree()
    {
        var analyzer = await Analyze(
            Combatant(talented: true),
            ApplyBuff(0, Spells.ApocalypticSurge.FSLID),
            RemoveBuff(999, Spells.ApocalypticSurge.FSLID),
            Detonate(1000));

        analyzer.FreeCasts.ShouldBe(1);
        analyzer.PaidCasts.ShouldBe(0);
        analyzer.SurgeStacksGained.ShouldBe(1);
        analyzer.SurgeStacksWasted.ShouldBe(0);
    }

    [Fact]
    public async Task WithoutTalentOrSurge_ReportsNoFreeCasts()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            Detonate(1000));

        analyzer.ApocalypticSurgeTalented.ShouldBeFalse();
        analyzer.FreeCasts.ShouldBe(0);
        analyzer.PaidCasts.ShouldBe(1);
        analyzer.SurgeStacksGained.ShouldBe(0);
        analyzer.SurgeStacksWasted.ShouldBe(0);
    }

    [Fact]
    public async Task Coverage_MarksActiveDoTsAndLeavesTheRestInactive()
    {
        var analyzer = await Analyze(
            Combatant(boomtasticRing: true),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.ApocalypseDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Coverage.Count.ShouldBe(ArdeosDots.Count);
        cast.Coverage.Select(entry => entry.Dot).ShouldBe(ArdeosDots.All);
        cast.DistinctDots.ShouldBe(2);

        Coverage(cast, ArdeosDots.SearingBlaze).Active.ShouldBeTrue();
        Coverage(cast, ArdeosDots.Apocalypse).Active.ShouldBeTrue();
        Coverage(cast, ArdeosDots.FireFrogs).Active.ShouldBeFalse();
        Coverage(cast, ArdeosDots.FireBall).Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Coverage_WithoutTheBoomtasticRing_OmitsApocalypse()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.ApocalypseDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Coverage.Count.ShouldBe(ArdeosDots.Count - 1);
        cast.Coverage.Select(entry => entry.Dot).ShouldNotContain(ArdeosDots.Apocalypse);
        cast.DistinctDots.ShouldBe(1);
        cast.TotalInstances.ShouldBe(1);
    }

    [Fact]
    public async Task Coverage_CountsEngulfingFlamesInstancesAndShowsNoCountForPresenceDoTs()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(150, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(200, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        var engulfing = Coverage(cast, ArdeosDots.EngulfingFlames);
        engulfing.Instances.ShouldBe(3);
        engulfing.Magnitude.ShouldBe(3);

        Coverage(cast, ArdeosDots.SearingBlaze).Magnitude.ShouldBeNull();
    }

    [Fact]
    public async Task Coverage_ReadsIncinerateStackCountAtEachCast()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.IncinerateDot.FSLID),
            ApplyDebuffStack(150, Enemy1, Spells.IncinerateDot.FSLID, stack: 5),
            Detonate(1000),
            ApplyDebuffStack(1500, Enemy1, Spells.IncinerateDot.FSLID, stack: 12),
            Detonate(2000));

        analyzer.Casts.Count.ShouldBe(2);

        var first = Coverage(analyzer.Casts[0], ArdeosDots.Incinerate);
        first.Instances.ShouldBe(1);
        first.Stacks.ShouldBe(5);
        first.Magnitude.ShouldBe(5);

        var second = Coverage(analyzer.Casts[1], ArdeosDots.Incinerate);
        second.Stacks.ShouldBe(12);
        second.Magnitude.ShouldBe(12);
    }

    [Fact]
    public async Task Coverage_AggregatesAcrossEveryEnemyCarryingTheEffect()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(120, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(100, Enemy2, Spells.EngulfingFlamesDot.FSLID),
            Detonate(1000));

        var engulfing = Coverage(analyzer.Casts.ShouldHaveSingleItem(), ArdeosDots.EngulfingFlames);
        engulfing.Targets.ShouldBe(2);
        engulfing.Instances.ShouldBe(3);
    }

    [Fact]
    public async Task Coverage_CountsAWindowRemovedOnTheCastTimestampAsActive()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            RemoveDebuff(1000, Enemy1, Spells.SearingBlazeDot.FSLID),
            Detonate(1000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        Coverage(cast, ArdeosDots.SearingBlaze).Active.ShouldBeTrue();
        cast.TotalInstances.ShouldBe(1);
    }

    [Fact]
    public async Task LayerTimeline_TotalAtEachCastMatchesThatCastsInstances()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(150, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(200, Enemy2, Spells.FireBallDot.FSLID),
            Detonate(1000),
            RemoveDebuff(1500, Enemy1, Spells.SearingBlazeDot.FSLID),
            Detonate(2000),
            Death(2500, Enemy2),
            Detonate(3000));

        analyzer.Casts.Count.ShouldBe(3);

        foreach (var cast in analyzer.Casts)
            TotalAt(analyzer, cast.Timestamp).ShouldBe(cast.TotalInstances);
    }

    [Fact]
    public async Task LayerTimeline_SpansThePullAndTracksEachEffectSeparately()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(100, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(150, Enemy1, Spells.EngulfingFlamesDot.FSLID),
            Detonate(1000));

        var timeline = analyzer.LayerTimeline;
        timeline.Count.ShouldBeGreaterThan(1);
        timeline[0].Timestamp.ShouldBe(analyzer.Pull.StartTime);
        timeline[^1].Timestamp.ShouldBe(analyzer.Pull.EndTime);
        timeline[0].Total.ShouldBe(0);

        var engulfingSlot = ArdeosDots.All.ToList().IndexOf(ArdeosDots.EngulfingFlames);
        SampleAt(analyzer, 1000).Instances[engulfingSlot].ShouldBe(2);
        analyzer.PeakLayeredInstances.ShouldBe(2);
    }

    private static DotCoverage Coverage(
        DetonateEfficiencyAnalyzer.DetonateCast cast, Dot dot) =>
        cast.Coverage.Single(entry => entry.Dot == dot);

    private static DotLayerSample SampleAt(DetonateEfficiencyAnalyzer analyzer, int timestamp) =>
        analyzer.LayerTimeline.Last(sample => sample.Timestamp <= timestamp);

    private static int TotalAt(DetonateEfficiencyAnalyzer analyzer, int timestamp) => SampleAt(analyzer, timestamp).Total;

    private static CombatantInfoEvent Combatant(bool talented = false, bool boomtasticRing = false) => new()
    {
        SourceId = PlayerId,
        Talents = talented ? [new TalentInfo { Id = ApocalypticSurgeTalentId }] : [],
        Gear = boomtasticRing
            ? [new Item { Id = FellowshipAnalyzer.Core.Common.Items.Items.RingOfBoomtasticExplosions.Id }]
            : [],
    };

    private static CastEvent Detonate(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = Spells.Detonate.FSLID },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int effectId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static ApplyDebuffStackEvent ApplyDebuffStack(int timestamp, int targetId, int effectId, int stack, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Stack = stack,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveDebuffEvent RemoveDebuff(int timestamp, int targetId, int effectId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static DeathEvent Death(int timestamp, int targetId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
        TargetInstance = instance,
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int effectId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int effectId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static async Task<DetonateEfficiencyAnalyzer> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        parser.Actors = Actors;
        await parser.Analyze([.. events], PlayerId, Dungeon);
        return parser.DetonateEfficiencyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
