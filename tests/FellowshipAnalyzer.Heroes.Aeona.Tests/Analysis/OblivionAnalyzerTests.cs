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

public sealed class OblivionAnalyzerTests
{
    private const int PlayerId = 310;
    private const int TankId = 90;
    private const int AllyId = 309;
    private const int EnemyId = 122;
    private const long TankMaxHitPoints = 40_000;
    private const int DungeonEndTime = 20_000;

    private static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(EnemyId, "Enemy", "NPC", "NPC", null, null),
    ];

    private static readonly List<ReportActor> ActorsWithoutTank =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(EnemyId, "Enemy", "NPC", "NPC", null, null),
    ];

    [Fact]
    public async Task Casts_AreRecordedInOrderWithTheEnemyTheyNamed()
    {
        var analyzer = await Analyze(OblivionCast(1_000), OblivionCast(5_000));

        analyzer.CastCount.ShouldBe(2);
        analyzer.Casts.Select(cast => cast.Timestamp).ShouldBe([1_000, 5_000]);
        analyzer.Casts.Select(cast => cast.TargetId).ShouldBe([EnemyId, EnemyId]);
    }

    [Fact]
    public async Task ShieldsFromOneCast_AreGroupedUnderThatCast()
    {
        var analyzer = await Analyze(
            OblivionCast(1_000),
            ShieldApplied(TankId, 1_001, absorb: 700),
            ShieldApplied(AllyId, 1_001, absorb: 300),
            ShieldApplied(PlayerId, 1_001, absorb: 250));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.AlliesShielded.ShouldBe(3);
        cast.ShieldApplied.ShouldBe(1_250);
        analyzer.ShieldApplied.ShouldBe(1_250);
        analyzer.ShieldAppliedPerCast.ShouldBe(1_250);
    }

    [Fact]
    public async Task ShieldFigures_AreAbsentWithoutOblivionsEmbrace()
    {
        var analyzer = await Analyze([],
            OblivionCast(1_000),
            ShieldApplied(TankId, 1_001, absorb: 700));

        analyzer.OblivionsEmbraceTalented.ShouldBeFalse();
        analyzer.ShieldApplied.ShouldBeNull();
        analyzer.ShieldAppliedPerCast.ShouldBeNull();
        analyzer.LargestSingleAbsorb.ShouldBeNull();
    }

    [Fact]
    public async Task LargestAbsorb_IsTheBiggestShareOfOneHitAShieldTook()
    {
        var analyzer = await Analyze(
            OblivionCast(1_000),
            ShieldApplied(TankId, 1_001, absorb: 5_000),
            Absorbed(TankId, 2_000, amount: 900),
            Absorbed(TankId, 3_000, amount: 2_400),
            Absorbed(TankId, 4_000, amount: 1_100));

        analyzer.LargestSingleAbsorb.ShouldBe(2_400);
        analyzer.Casts.ShouldHaveSingleItem().LargestSingleAbsorb.ShouldBe(2_400);
    }

    [Fact]
    public async Task AFreeCast_ReadsItsSourceFromTheLog()
    {
        var analyzer = await Analyze([AeonaTalents.OblivionsEmbrace, AeonaTalents.Uchronia],
            UchroniaApplied(1_000),
            FreeOblivionCast(2_000),
            UchroniaRemoved(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.WasFree.ShouldBeTrue();
        cast.FreeCastSource.ShouldBe(FreeCastSource.Uchronia);
        analyzer.FreeCasts.ShouldBe(1);
    }

    [Fact]
    public async Task ACastThatCostChrona_IsNotCountedAsFree()
    {
        var analyzer = await Analyze(OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.WasFree.ShouldBeFalse();
        cast.FreeCastSource.ShouldBeNull();
        analyzer.FreeCasts.ShouldBe(0);
    }

    [Fact]
    public async Task FreeCastOpportunities_CountEveryUchroniaAndEpochBreakWindow()
    {
        var analyzer = await Analyze([AeonaTalents.OblivionsEmbrace, AeonaTalents.Uchronia],
            UchroniaApplied(1_000),
            FreeOblivionCast(1_500),
            UchroniaRemoved(1_500),
            EpochBreakApplied(4_000),
            EpochBreakRemoved(8_000),
            UchroniaApplied(9_000),
            UchroniaRemoved(9_500));

        analyzer.FreeCastOpportunities.ShouldBe(3);
        analyzer.FreeCasts.ShouldBe(1);
    }

    [Fact]
    public async Task ACastAboveFortyPercentStaggerWithACleanseReady_IsFlagged()
    {
        var analyzer = await Analyze(
            TankStaggerSnapshot(1_900, staggerHitPoints: 18_000),
            OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.TankStaggerFraction.ShouldBe(0.45);
        cast.CleanseAvailable.ShouldBeTrue();
        cast.AtCleansePriority.ShouldBeTrue();
        cast.Rated.ShouldBeTrue();
        analyzer.CastsAtCleansePriority.ShouldBe(1);
        analyzer.CastsRated.ShouldBe(1);
    }

    [Fact]
    public async Task ACastBelowFortyPercentStagger_IsNotFlagged()
    {
        var analyzer = await Analyze(
            TankStaggerSnapshot(1_900, staggerHitPoints: 12_000),
            OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.TankStaggerFraction.ShouldBe(0.30);
        cast.AtCleansePriority.ShouldBeFalse();
        analyzer.CastsAtCleansePriority.ShouldBe(0);
        analyzer.CastsRated.ShouldBe(1);
    }

    [Fact]
    public async Task ACastWithADeadTank_IsNotFlagged()
    {
        var analyzer = await Analyze(
            TankStaggerSnapshot(1_800, staggerHitPoints: 18_000),
            TankDeath(1_900),
            OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.CleanseAvailable.ShouldBeFalse();
        cast.AtCleansePriority.ShouldBeFalse();
        analyzer.CastsAtCleansePriority.ShouldBe(0);
    }

    [Fact]
    public async Task ACastWithStaleStagger_IsNotRated()
    {
        var analyzer = await Analyze(
            TankStaggerSnapshot(500, staggerHitPoints: 18_000),
            OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.TankStaggerFraction.ShouldBeNull();
        cast.Rated.ShouldBeFalse();
        cast.AtCleansePriority.ShouldBeFalse();
        analyzer.CastsRated.ShouldBe(0);
    }

    [Fact]
    public async Task WithNoTankInTheReport_NoCastIsRated()
    {
        var analyzer = await Analyze(ActorsWithoutTank, [AeonaTalents.OblivionsEmbrace], OblivionCast(2_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.TankStaggerFraction.ShouldBeNull();
        cast.Rated.ShouldBeFalse();
        analyzer.CastsRated.ShouldBe(0);
    }

    [Fact]
    public async Task WithNoCast_EveryFigureReadsEmpty()
    {
        var analyzer = await Analyze(TankStaggerSnapshot(1_000, staggerHitPoints: 10_000));

        analyzer.CastCount.ShouldBe(0);
        analyzer.Casts.ShouldBeEmpty();
        analyzer.ShieldAppliedPerCast.ShouldBeNull();
        analyzer.CastsAtCleansePriority.ShouldBe(0);
        analyzer.CastsRated.ShouldBe(0);
    }

    [Fact]
    public async Task TheAnalyzerIsReachableOnEveryPullReadPath()
    {
        var parser = await AnalyzeAsync(Actors, [Talented(AeonaTalents.OblivionsEmbrace), OblivionCast(1_000)]);

        var entry = parser.OblivionAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<OblivionAnalyzer>();
        entry.Pull.OblivionAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(entry.Pull).OblivionAnalyzer.ShouldBeSameAs(analyzer);
    }

    private static CastEvent OblivionCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = Spells.Oblivion.FSLID },
    };

    private static FreeCastEvent FreeOblivionCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        AbilityGameId = Spells.Oblivion.FSLID,
        Ability = new Ability { Id = Spells.Oblivion.FSLID },
    };

    private static ApplyBuffEvent ShieldApplied(int unitId, int timestamp, int absorb) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Absorb = absorb,
        Ability = new Ability { Id = Spells.OblivionAbsorbAbsorb.FSLID },
    };

    private static AbsorbedEvent Absorbed(int unitId, int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        AttackerId = EnemyId,
        Amount = amount,
        Ability = new Ability { Id = Spells.OblivionAbsorbAbsorb.FSLID },
    };

    private static ApplyBuffEvent UchroniaApplied(int timestamp) =>
        SelfBuffApplied(timestamp, Spells.Uchronia.FSLID);

    private static RemoveBuffEvent UchroniaRemoved(int timestamp) =>
        SelfBuffRemoved(timestamp, Spells.Uchronia.FSLID);

    private static ApplyBuffEvent EpochBreakApplied(int timestamp) =>
        SelfBuffApplied(timestamp, Spells.EpochBreakSelfBuff.FSLID);

    private static RemoveBuffEvent EpochBreakRemoved(int timestamp) =>
        SelfBuffRemoved(timestamp, Spells.EpochBreakSelfBuff.FSLID);

    private static ApplyBuffEvent SelfBuffApplied(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static RemoveBuffEvent SelfBuffRemoved(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static HealEvent TankStaggerSnapshot(int timestamp, int staggerHitPoints) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Amount = 1_000,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = new ActorResources
        {
            HitPoints = TankMaxHitPoints / 2,
            MaxHitPoints = TankMaxHitPoints,
            Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = staggerHitPoints * 100, Max = -100 }],
        },
    };

    private static DeathEvent TankDeath(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = TankId,
        TargetId = TankId,
    };

    private static CombatantInfoEvent Talented(params int[] talentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talentIds.Select(id => new TalentInfo { Id = id })],
    };

    private static Task<OblivionAnalyzer> Analyze(params Event[] events) =>
        Analyze([AeonaTalents.OblivionsEmbrace], events);

    private static Task<OblivionAnalyzer> Analyze(int[] talents, params Event[] events) =>
        Analyze(Actors, talents, events);

    private static async Task<OblivionAnalyzer> Analyze(
        List<ReportActor> actors, int[] talents, params Event[] events)
    {
        var parser = await AnalyzeAsync(actors, [Talented(talents), .. events]);

        return parser.OblivionAnalyzers
            .ShouldHaveSingleItem()
            .Analyzer
            .ShouldBeOfType<OblivionAnalyzer>();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(List<ReportActor> actors, Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        parser.Actors = actors;
        await parser.Analyze(
            [.. events],
            PlayerId,
            new ReportDungeon(0, "", 1, null, 0, DungeonEndTime, null, null, null));

        return parser;
    }
}
