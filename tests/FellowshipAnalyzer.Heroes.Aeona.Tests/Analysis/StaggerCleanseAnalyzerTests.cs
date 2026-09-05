using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
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

public sealed class StaggerCleanseAnalyzerTests
{
    private const int PlayerId = 310;
    private const int TankId = 90;
    private const int AllyId = 309;
    private const int SecondAllyId = 293;
    private const long MaxHitPoints = 40000;
    private const int DungeonEndTime = 30_000;

    private static readonly List<ReportActor> Party =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(SecondAllyId, "Mara", "Player", "Mara", null, null),
    ];

    private static readonly List<ReportActor> PartyWithoutTank =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
    ];

    [Fact]
    public async Task StaggerCleansed_ComesFromTheReadingsEitherSideOfTheCast()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0, staggerHitPointsAfter: 8_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.Ability.ShouldBe(Spells.AmendFate.FSLID);
        cast.AlliesHealed.ShouldBe(1);
        cast.StaggerCleansed.ShouldBe(2_000);
        cast.Heals.ShouldHaveSingleItem().StaggerCleansed.ShouldBe(2_000);
    }

    [Fact]
    public async Task StaggerCleansed_IsWithheldWithoutAReadingAfterTheCast()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0));

        analyzer.Casts.ShouldHaveSingleItem().StaggerCleansed.ShouldBeNull();
    }

    [Fact]
    public async Task StaggerCleansed_IsWithheldWhenADrainTickMovedThePoolInsideTheBracket()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            StaggerDrainTick(TankId, 2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0, staggerHitPointsAfter: 8_000));

        analyzer.Casts.ShouldHaveSingleItem().StaggerCleansed.ShouldBeNull();
    }

    [Fact]
    public async Task EffectiveHealingAndOverheal_AreSummedOverEveryAllyTheCastHealed()
    {
        var analyzer = await Analyze(
            RestoreContinuityCast(2_000),
            RestoreContinuityHeal(TankId, 2_001, effective: 4_000, overheal: 500),
            RestoreContinuityHeal(AllyId, 2_002, effective: 3_000, overheal: 0),
            RestoreContinuityHeal(SecondAllyId, 2_003, effective: 2_000, overheal: 250));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.AlliesHealed.ShouldBe(3);
        cast.EffectiveHealing.ShouldBe(9_000);
        cast.Overheal.ShouldBe(750);
        analyzer.EffectiveHealing.ShouldBe(9_000);
        analyzer.Overheal.ShouldBe(750);
    }

    [Fact]
    public async Task PerAbilityTotals_CountOnlyTheCastsTheReadingsBracket()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0, staggerHitPointsAfter: 8_000),

            AmendFateCast(6_000),
            AmendFateHeal(TankId, 6_001, effective: 5_500, overheal: 0));

        analyzer.AmendFateCasts.ShouldBe(2);
        analyzer.MeasuredCastsOf(Spells.AmendFate.FSLID).ShouldBe(1);
        analyzer.StaggerCleansedBy(Spells.AmendFate.FSLID).ShouldBe(2_000);
        analyzer.StaggerCleansedBy(Spells.RestoreContinuity.FSLID).ShouldBe(0);
    }

    [Fact]
    public async Task ACastOnAPoolBelowOneCleanse_IsALowStaggerCast()
    {
        var analyzer = await Analyze(FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            Snapshot(TankId, 5_000, staggerHitPoints: 1_200),
            AmendFateCast(5_500),
            AmendFateHeal(TankId, 5_501, effective: 3_300, overheal: 0, staggerHitPointsAfter: 0),
        ]).ToArray());

        var casts = analyzer.Casts;

        casts[0].SingleCastCleanseAmount.ShouldBe(2_000);
        casts[0].BelowSingleCleanse.ShouldBe(false);
        casts[1].StaggerBefore.ShouldBe(1_200);
        casts[1].BelowSingleCleanse.ShouldBe(true);

        analyzer.LowStaggerCasts.ShouldBe(1);
        analyzer.CastsWithStaggerReading.ShouldBe(2);
    }

    [Fact]
    public async Task ACastWithAStaleReading_IsNotJudged()
    {
        var analyzer = await Analyze(FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            Snapshot(TankId, 5_000, staggerHitPoints: 1_200),
            AmendFateCast(8_000),
            AmendFateHeal(TankId, 8_001, effective: 3_300, overheal: 0),
        ]).ToArray());

        var cast = analyzer.Casts[1];

        cast.StaggerBefore.ShouldBeNull();
        cast.BelowSingleCleanse.ShouldBeNull();
        analyzer.CastsWithStaggerReading.ShouldBe(1);
    }

    [Fact]
    public async Task WithNoCleanCastInTheReport_NoCastIsJudged()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.SingleCastCleanseAmount.ShouldBeNull();
        cast.BelowSingleCleanse.ShouldBeNull();
        analyzer.CastsWithStaggerReading.ShouldBe(0);
    }

    [Fact]
    public async Task RestoreContinuity_IsJudgedOnTheHealedAllyHoldingTheMostStagger()
    {
        var analyzer = await AnalyzeWith(Party, [],
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            RestoreContinuityCast(2_000),
            RestoreContinuityHeal(TankId, 2_001, effective: 4_000, overheal: 0, staggerHitPointsAfter: 8_000),

            Snapshot(TankId, 5_000, staggerHitPoints: 900),
            Snapshot(AllyId, 5_100, staggerHitPoints: 7_500),
            RestoreContinuityCast(5_500),
            RestoreContinuityHeal(TankId, 5_501, effective: 1_000, overheal: 0),
            RestoreContinuityHeal(AllyId, 5_502, effective: 4_000, overheal: 0));

        var cast = analyzer.Casts[1];

        cast.StaggerBefore.ShouldBe(7_500);
        cast.BelowSingleCleanse.ShouldBe(false);
    }

    [Fact]
    public async Task AFreeCastOnAFullPool_Passes()
    {
        var analyzer = await AnalyzeWith(Party, [AeonaTalents.Uchronia], FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            UchroniaApplied(5_000),
            Snapshot(TankId, 5_100, staggerHitPoints: 9_000),
            FreeAmendFateCast(5_500),
            AmendFateHeal(TankId, 5_501, effective: 5_500, overheal: 0),
            UchroniaRemoved(5_500),
        ]).ToArray());

        var cast = analyzer.Casts[1];

        cast.WasFree.ShouldBeTrue();
        cast.FreeCastSource.ShouldBe(FreeCastSource.Uchronia);
        cast.FreeCastOnFullPool.ShouldBe(true);
        analyzer.FreeCleanseCasts.ShouldBe(1);
        analyzer.FreeCleanseCastsOnFullPool.ShouldBe(1);
        analyzer.FreeCleanseCastsWithStaggerReading.ShouldBe(1);
    }

    [Fact]
    public async Task AFreeCastBelowOneCleanse_Fails()
    {
        var analyzer = await Analyze(FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            Snapshot(TankId, 5_100, staggerHitPoints: 800),
            FreeAmendFateCast(5_500),
            AmendFateHeal(TankId, 5_501, effective: 2_200, overheal: 0),
        ]).ToArray());

        var cast = analyzer.Casts[1];

        cast.FreeCastOnFullPool.ShouldBe(false);
        analyzer.FreeCleanseCastsOnFullPool.ShouldBe(0);
        analyzer.FreeCleanseCastsWithStaggerReading.ShouldBe(1);
    }

    [Fact]
    public async Task APaidCast_CarriesNoFreeCastVerdict()
    {
        var analyzer = await Analyze(
            Snapshot(TankId, 1_000, staggerHitPoints: 10_000),
            AmendFateCast(2_000),
            AmendFateHeal(TankId, 2_001, effective: 5_500, overheal: 0, staggerHitPointsAfter: 8_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.WasFree.ShouldBeFalse();
        cast.FreeCastSource.ShouldBeNull();
        cast.FreeCastOnFullPool.ShouldBeNull();
        analyzer.FreeCleanseCasts.ShouldBe(0);
    }

    [Fact]
    public async Task EchoesOfDivinity_IsAbsentWithoutTheTalent()
    {
        var analyzer = await Analyze(
            EchoesApplied(TankId, 1_000),
            EchoesRemoved(TankId, 5_000));

        analyzer.EchoesOfDivinity.ShouldBeNull();
    }

    [Fact]
    public async Task EchoesOfDivinity_IsAbsentWhenTheReportNamesNoTank()
    {
        var analyzer = await AnalyzeWith(PartyWithoutTank, [AeonaTalents.EchoesOfDivinity],
            EchoesApplied(AllyId, 1_000),
            EchoesRemoved(AllyId, 5_000));

        analyzer.EchoesOfDivinity.ShouldBeNull();
    }

    [Fact]
    public async Task EchoesOfDivinity_MeasuresTheTankAloneAndIgnoresPartyApplications()
    {
        var analyzer = await AnalyzeWith(Party, [AeonaTalents.EchoesOfDivinity],
            EchoesApplied(TankId, 1_000),
            EchoesApplied(AllyId, 1_000),
            EchoesRemoved(TankId, 5_000),
            EchoesRemoved(AllyId, 9_000));

        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();

        echoes.Applications.ShouldBe(1);
        echoes.ActiveMs.ShouldBe(4_000);
        echoes.Windows.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task EchoesOfDivinity_ClosesAWindowStillOpenAtThePullEnd()
    {
        var analyzer = await AnalyzeWith(Party, [AeonaTalents.EchoesOfDivinity],
            EchoesApplied(TankId, 1_000));

        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();

        echoes.Windows.ShouldHaveSingleItem().End.ShouldBe(DungeonEndTime);
    }

    [Fact]
    public async Task EchoesOfDivinity_CountsAnOverwriteByALowStaggerCastAlone()
    {
        var analyzer = await AnalyzeWith(Party, [AeonaTalents.EchoesOfDivinity], FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            EchoesApplied(TankId, 2_001),

            Snapshot(TankId, 5_000, staggerHitPoints: 9_000),
            AmendFateCast(5_500),
            AmendFateHeal(TankId, 5_501, effective: 5_500, overheal: 0),
            EchoesRefreshed(TankId, 5_501),

            Snapshot(TankId, 8_000, staggerHitPoints: 900),
            AmendFateCast(8_500),
            AmendFateHeal(TankId, 8_501, effective: 2_400, overheal: 0),
            EchoesRefreshed(TankId, 8_501),
        ]).ToArray());

        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();

        echoes.Refreshes.ShouldBe(2);
        echoes.Overwrites.ShouldBe(1);
        analyzer.Casts[1].OverwroteEchoes.ShouldBeFalse();
        analyzer.Casts[2].OverwroteEchoes.ShouldBeTrue();
    }

    [Fact]
    public async Task EchoesOfDivinity_MeasuresTheTimeAnOverwriteDiscarded()
    {
        var analyzer = await AnalyzeWith(Party, [AeonaTalents.EchoesOfDivinity], FullValueCast(1_000, 10_000, 8_000).Concat(
        [
            EchoesApplied(TankId, 2_001),
            EchoesRemoved(TankId, 6_001),

            EchoesApplied(TankId, 10_000),
            Snapshot(TankId, 11_000, staggerHitPoints: 900),
            AmendFateCast(11_500),
            AmendFateHeal(TankId, 11_501, effective: 2_400, overheal: 0),
            EchoesRefreshed(TankId, 11_501),
        ]).ToArray());

        var echoes = analyzer.EchoesOfDivinity.ShouldNotBeNull();

        echoes.Overwrites.ShouldBe(1);
        echoes.OverwrittenMs.ShouldBe(2_499);
        analyzer.Casts[1].EchoesOverwrittenMs.ShouldBe(2_499);
    }

    private static Event[] FullValueCast(int castTimestamp, int staggerBefore, int staggerAfter) =>
    [
        Snapshot(TankId, castTimestamp - 100, staggerHitPoints: staggerBefore),
        AmendFateCast(castTimestamp),
        AmendFateHeal(TankId, castTimestamp + 1, effective: 5_500, overheal: 0, staggerHitPointsAfter: staggerAfter),
    ];

    private static ActorResources StaggerResources(int staggerHitPoints) => new()
    {
        HitPoints = MaxHitPoints / 2,
        MaxHitPoints = MaxHitPoints,
        Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = staggerHitPoints * 100, Max = -100 }],
    };

    private static HealEvent Snapshot(int unitId, int timestamp, int staggerHitPoints) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Amount = 1,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = StaggerResources(staggerHitPoints),
    };

    private static DamageEvent StaggerDrainTick(int unitId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = unitId,
        TargetId = unitId,
        Amount = 181,
        Tick = true,
        Ability = new Ability { Id = 1002696 },
    };

    private static CastEvent AmendFateCast(int timestamp) => Cast(timestamp, Spells.AmendFate.FSLID);

    private static CastEvent RestoreContinuityCast(int timestamp) => Cast(timestamp, Spells.RestoreContinuity.FSLID);

    private static CastEvent Cast(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = abilityId },
    };

    private static FreeCastEvent FreeAmendFateCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.AmendFate.FSLID },
        AbilityGameId = Spells.AmendFate.FSLID,
    };

    private static HealEvent AmendFateHeal(
        int unitId, int timestamp, long effective, long overheal, int? staggerHitPointsAfter = null) =>
        CleanseHeal(unitId, timestamp, Spells.AmendFate.FSLID, effective, overheal, staggerHitPointsAfter);

    private static HealEvent RestoreContinuityHeal(
        int unitId, int timestamp, long effective, long overheal, int? staggerHitPointsAfter = null) =>
        CleanseHeal(unitId, timestamp, Spells.RestoreContinuity.FSLID, effective, overheal, staggerHitPointsAfter);

    private static HealEvent CleanseHeal(
        int unitId, int timestamp, int abilityId, long effective, long overheal, int? staggerHitPointsAfter) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = unitId,
            Amount = effective,
            Overheal = overheal == 0 ? null : overheal,
            Ability = new Ability { Id = abilityId },
            TargetResources = staggerHitPointsAfter is { } stagger ? StaggerResources(stagger) : null,
        };

    private static ApplyBuffEvent EchoesApplied(int unitId, int timestamp) =>
        Applied(unitId, timestamp, Spells.EchoesOfDivinity.FSLID);

    private static RefreshBuffEvent EchoesRefreshed(int unitId, int timestamp) =>
        Refreshed(unitId, timestamp, Spells.EchoesOfDivinity.FSLID);

    private static RemoveBuffEvent EchoesRemoved(int unitId, int timestamp) =>
        Removed(unitId, timestamp, Spells.EchoesOfDivinity.FSLID);

    private static ApplyBuffEvent UchroniaApplied(int timestamp) =>
        Applied(PlayerId, timestamp, Spells.Uchronia.FSLID);

    private static RemoveBuffEvent UchroniaRemoved(int timestamp) =>
        Removed(PlayerId, timestamp, Spells.Uchronia.FSLID);

    private static ApplyBuffEvent Applied(int unitId, int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Ability = new Ability { Id = abilityId },
    };

    private static RefreshBuffEvent Refreshed(int unitId, int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Ability = new Ability { Id = abilityId },
    };

    private static RemoveBuffEvent Removed(int unitId, int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Ability = new Ability { Id = abilityId },
    };

    private static CombatantInfoEvent Combatant(int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(talent => new TalentInfo { Id = talent })],
    };

    private static Task<StaggerCleanseAnalyzer> Analyze(params Event[] events) => AnalyzeWith(Party, [], events);

    private static async Task<StaggerCleanseAnalyzer> AnalyzeWith(List<ReportActor> actors, int[] talents, params Event[] events)
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
            [Combatant(talents), .. events],
            PlayerId,
            new ReportDungeon(0, "", 1, null, 0, DungeonEndTime, null, null, null));

        return parser.StaggerCleanseAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<StaggerCleanseAnalyzer>();
    }
}
