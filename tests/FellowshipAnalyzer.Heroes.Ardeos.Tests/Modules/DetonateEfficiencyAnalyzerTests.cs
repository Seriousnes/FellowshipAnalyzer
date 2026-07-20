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

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class DetonateEfficiencyAnalyzerTests
{
    private const int PlayerId = 7;
    private const int Enemy1 = 100;
    private const int Enemy2 = 200;
    private const int Instance = 1;
    private const int ApocalypticSurgeTalentId = 678;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

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
    public async Task ApocalypticSurge_ExpiryWithoutDetonate_IsWasted()
    {
        var analyzer = await Analyze(
            Combatant(talented: true),
            ApplyBuff(0, Spells.ApocalypticSurge.FSLID),
            ApplyBuffStack(0, Spells.ApocalypticSurge.FSLID, stack: 2),
            Detonate(1000),
            RemoveBuff(5000, Spells.ApocalypticSurge.FSLID));

        analyzer.FreeCasts.ShouldBe(0);
        analyzer.PaidCasts.ShouldBe(1);
        analyzer.SurgeStacksGained.ShouldBe(2);
        analyzer.SurgeStacksWasted.ShouldBe(2);
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

    private static CombatantInfoEvent Combatant(bool talented = false) => new()
    {
        SourceId = PlayerId,
        Talents = talented ? [new TalentInfo { Id = ApocalypticSurgeTalentId }] : [],
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
        await parser.Analyze([.. events], PlayerId, Fight);
        return parser.DetonateEfficiencyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
