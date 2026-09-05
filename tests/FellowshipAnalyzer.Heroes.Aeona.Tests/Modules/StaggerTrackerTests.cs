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

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class StaggerTrackerTests
{
    private const int PlayerId = 310;
    private const int TankId = 90;
    private const int AllyId = 309;
    private const int EnemyId = 122;
    private const long TankMaxHitPoints = 36268;
    private const int StaggerDrainFslid = 1002696;

    private static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(EnemyId, "Enemy", "NPC", "NPC", null, null),
    ];

    [Fact]
    public async Task Snapshots_AreChronologicalWithConsecutiveDuplicatesCollapsed()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 962489),
            TankSnapshotHeal(2000, rawStagger: 962489),
            TankSnapshotHeal(3000, rawStagger: 778947),
            TankSnapshotHeal(4000, rawStagger: 778947),
            TankSnapshotHeal(5000, rawStagger: 400000));

        var snapshots = tracker.SnapshotsFor(TankId);

        snapshots.Select(snapshot => snapshot.Timestamp).ShouldBe([1000, 3000, 5000]);
        snapshots.Select(snapshot => snapshot.Amount).ShouldBe([9624, 7789, 4000]);
    }

    [Fact]
    public async Task Snapshots_CarryTheNormalizedScale()
    {
        var tracker = await Track(TankSnapshotHeal(1000, rawStagger: 962489));

        var snapshot = tracker.SnapshotsFor(TankId).ShouldHaveSingleItem();

        snapshot.Amount.ShouldBe(9624);
        snapshot.Max.ShouldBe(-1);
        snapshot.MaxHitPoints.ShouldBe(TankMaxHitPoints);
    }

    [Fact]
    public async Task Snapshots_AreKeptPerUnit()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 500000),
            SnapshotHeal(AllyId, 2000, rawStagger: 100000),
            PlayerSnapshotCast(3000, rawStagger: 3400));

        tracker.TrackedUnitIds.ShouldBe([TankId, AllyId, PlayerId]);
        tracker.SnapshotsFor(TankId).ShouldHaveSingleItem().Amount.ShouldBe(5000);
        tracker.SnapshotsFor(AllyId).ShouldHaveSingleItem().Amount.ShouldBe(1000);
        tracker.SnapshotsFor(PlayerId).ShouldHaveSingleItem().Amount.ShouldBe(34);
        tracker.SnapshotsFor(EnemyId).ShouldBeEmpty();
    }

    [Fact]
    public async Task LatestBefore_TakesTheReadingStrictlyEarlier()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 500000),
            TankSnapshotHeal(3000, rawStagger: 300000),
            TankSnapshotHeal(5000, rawStagger: 100000));

        tracker.LatestBefore(TankId, 3000)!.Timestamp.ShouldBe(1000);
        tracker.LatestBefore(TankId, 3001)!.Timestamp.ShouldBe(3000);
        tracker.LatestBefore(TankId, 6000)!.Timestamp.ShouldBe(5000);
        tracker.LatestBefore(TankId, 1000).ShouldBeNull();
        tracker.LatestBefore(EnemyId, 6000).ShouldBeNull();
    }

    [Fact]
    public async Task EarliestAfter_TakesTheReadingAtOrLater()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 500000),
            TankSnapshotHeal(3000, rawStagger: 300000),
            TankSnapshotHeal(5000, rawStagger: 100000));

        tracker.EarliestAfter(TankId, 3000)!.Timestamp.ShouldBe(3000);
        tracker.EarliestAfter(TankId, 3001)!.Timestamp.ShouldBe(5000);
        tracker.EarliestAfter(TankId, 0)!.Timestamp.ShouldBe(1000);
        tracker.EarliestAfter(TankId, 5001).ShouldBeNull();
    }

    [Fact]
    public async Task TankId_ResolvesTheTankFromTheReportActors()
    {
        var tracker = await Track(TankSnapshotHeal(1000, rawStagger: 500000));

        tracker.TankIds.ShouldBe([TankId]);
        tracker.TankId.ShouldBe(TankId);
    }

    [Fact]
    public async Task TankId_IsNullWhenTheReportNamesNoTank()
    {
        var tracker = await Track([new(PlayerId, "Aeona", "Player", "Aeona", null, null)], TankSnapshotHeal(1000, rawStagger: 500000));

        tracker.TankIds.ShouldBeEmpty();
        tracker.TankId.ShouldBeNull();
    }

    [Fact]
    public async Task StaggerFractionOfMaxHp_MeasuresTheUncappedPoolAgainstMaximumHitPoints()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 1000, rawStagger: 1_600_000, maxHitPoints: 40000),
            SnapshotHeal(TankId, 3000, rawStagger: 8_000_000, maxHitPoints: 40000));

        tracker.StaggerFractionOfMaxHp(TankId, 2000)!.Value.ShouldBe(0.4, 0.0001);
        tracker.StaggerFractionOfMaxHp(TankId, 4000)!.Value.ShouldBe(2.0, 0.0001);
        tracker.StaggerFractionOfMaxHp(TankId, 1000).ShouldBeNull();
        tracker.StaggerFractionOfMaxHp(EnemyId, 4000).ShouldBeNull();
    }

    [Fact]
    public async Task MaxHitPointsOf_FallsBackToTheNearestLaterReading()
    {
        var tracker = await Track(SnapshotHeal(TankId, 3000, rawStagger: 500000, maxHitPoints: 40000));

        tracker.MaxHitPointsOf(TankId, 1000).ShouldBe(40000);
        tracker.MaxHitPointsOf(TankId, 5000).ShouldBe(40000);
        tracker.MaxHitPointsOf(EnemyId, 5000).ShouldBeNull();
    }

    [Fact]
    public async Task MeasureCleanse_ReportsTheClearedStaggerWithNoInterference()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 962489),
            AmendFateCast(2000),
            AmendFateHeal(TankId, 2001, rawStagger: 778947));

        var cleanse = tracker.MeasureCleanse(TankId, 2000, windowMs: 500).ShouldNotBeNull();

        cleanse.UnitId.ShouldBe(TankId);
        cleanse.CastTimestamp.ShouldBe(2000);
        cleanse.PreTimestamp.ShouldBe(1000);
        cleanse.PreAmount.ShouldBe(9624);
        cleanse.PostTimestamp.ShouldBe(2001);
        cleanse.PostAmount.ShouldBe(7789);
        cleanse.ClearedAmount.ShouldBe(1835);
        cleanse.InterveningTickCount.ShouldBe(0);
        cleanse.InterveningCleanseCount.ShouldBe(0);
        cleanse.HasInterveningEvent.ShouldBeFalse();
    }

    [Fact]
    public async Task MeasureCleanse_CountsAnInterveningDrainTick()
    {
        var tracker = await Track(
            SnapshotHeal(PlayerId, 1000, rawStagger: 100000),
            StaggerDrainTick(PlayerId, 1500),
            RestoreContinuityCast(2000),
            RestoreContinuityHeal(PlayerId, 2001, rawStagger: 20000));

        var cleanse = tracker.MeasureCleanse(PlayerId, 2000, windowMs: 500).ShouldNotBeNull();

        cleanse.ClearedAmount.ShouldBe(800);
        cleanse.InterveningTickCount.ShouldBe(1);
        cleanse.HasInterveningEvent.ShouldBeTrue();
    }

    [Fact]
    public async Task MeasureCleanse_CountsAnotherCleanseInsideTheBracket()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 962489),
            AmendFateCast(1500),
            RestoreContinuityCast(2000),
            RestoreContinuityHeal(TankId, 2001, rawStagger: 500000));

        var cleanse = tracker.MeasureCleanse(TankId, 2000, windowMs: 500).ShouldNotBeNull();

        cleanse.InterveningCleanseCount.ShouldBe(1);
        cleanse.InterveningTickCount.ShouldBe(0);
        cleanse.HasInterveningEvent.ShouldBeTrue();
    }

    [Fact]
    public async Task MeasureCleanse_IsNullWhenNoReadingLandsInTheWindow()
    {
        var tracker = await Track(
            TankSnapshotHeal(1000, rawStagger: 962489),
            AmendFateCast(2000),
            AmendFateHeal(TankId, 9000, rawStagger: 778947));

        tracker.MeasureCleanse(TankId, 2000, windowMs: 500).ShouldBeNull();
    }

    [Fact]
    public async Task MeasureCleanse_IsNullWithoutAReadingAheadOfTheCast()
    {
        var tracker = await Track(
            AmendFateCast(2000),
            AmendFateHeal(TankId, 2001, rawStagger: 778947));

        tracker.MeasureCleanse(TankId, 2000, windowMs: 500).ShouldBeNull();
    }

    [Fact]
    public async Task CleanseCasts_RecordTheHealTargetsTheLogNamesInsteadOfTheCastTarget()
    {
        var tracker = await Track(
            AmendFateCast(1000),
            AmendFateHeal(TankId, 1001, rawStagger: 500000),
            RestoreContinuityCast(4000),
            RestoreContinuityHeal(TankId, 4001, rawStagger: 300000),
            RestoreContinuityHeal(AllyId, 4001, rawStagger: 100000),
            RestoreContinuityHeal(PlayerId, 4001, rawStagger: 5000));

        tracker.CleanseCasts.Count.ShouldBe(2);
        tracker.CleanseCasts[0].Ability.ShouldBe(Spells.AmendFate.FSLID);
        tracker.CleanseCasts[0].TargetId.ShouldBe(-1);
        tracker.CleanseCasts[0].HealTargets.ShouldBe([TankId]);
        tracker.CleanseCasts[1].Ability.ShouldBe(Spells.RestoreContinuity.FSLID);
        tracker.CleanseCasts[1].HealTargets.ShouldBe([TankId, AllyId, PlayerId]);
    }

    [Fact]
    public async Task CleanseCastsBetween_TakesBothBoundsInclusively()
    {
        var tracker = await Track(
            AmendFateCast(1000),
            AmendFateCast(2000),
            RestoreContinuityCast(3000),
            AmendFateCast(4000));

        tracker.CleanseCastsBetween(2000, 3000).Select(cast => cast.Timestamp).ShouldBe([2000, 3000]);
        tracker.CleanseCastsBetween(5000, 6000).ShouldBeEmpty();
    }

    [Fact]
    public async Task DrainTicks_AreRecordedForTheUnitTheyDrain()
    {
        var tracker = await Track(
            StaggerDrainTick(PlayerId, 1000),
            StaggerDrainTick(PlayerId, 4000));

        tracker.DrainTicksFor(PlayerId).ShouldBe([1000, 4000]);
        tracker.DrainTicksFor(TankId).ShouldBeEmpty();
    }

    private static ActorResources Resources(int rawStagger, long maxHitPoints) => new()
    {
        HitPoints = maxHitPoints / 2,
        MaxHitPoints = maxHitPoints,
        Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = rawStagger, Max = -100 }],
    };

    private static HealEvent TankSnapshotHeal(int timestamp, int rawStagger) =>
        SnapshotHeal(TankId, timestamp, rawStagger);

    private static HealEvent SnapshotHeal(int unitId, int timestamp, int rawStagger, long maxHitPoints = TankMaxHitPoints) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = Resources(rawStagger, maxHitPoints),
    };

    private static CastEvent PlayerSnapshotCast(int timestamp, int rawStagger) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.Oblivion.FSLID },
        SourceResources = Resources(rawStagger, TankMaxHitPoints),
    };

    private static CastEvent AmendFateCast(int timestamp) => CleanseCast(timestamp, Spells.AmendFate.FSLID);

    private static CastEvent RestoreContinuityCast(int timestamp) => CleanseCast(timestamp, Spells.RestoreContinuity.FSLID);

    private static CastEvent CleanseCast(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = abilityId },
    };

    private static HealEvent AmendFateHeal(int unitId, int timestamp, int rawStagger) =>
        CleanseHeal(unitId, timestamp, rawStagger, Spells.AmendFate.FSLID);

    private static HealEvent RestoreContinuityHeal(int unitId, int timestamp, int rawStagger) =>
        CleanseHeal(unitId, timestamp, rawStagger, Spells.RestoreContinuity.FSLID);

    private static HealEvent CleanseHeal(int unitId, int timestamp, int rawStagger, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Amount = 5000,
        Ability = new Ability { Id = abilityId },
        TargetResources = Resources(rawStagger, TankMaxHitPoints),
    };

    private static DamageEvent StaggerDrainTick(int unitId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = unitId,
        TargetId = unitId,
        Amount = 181,
        UnmitigatedAmount = 246,
        Tick = true,
        Ability = new Ability { Id = StaggerDrainFslid },
    };

    private static Task<StaggerTracker> Track(params Event[] events) => Track(Actors, events);

    private static async Task<StaggerTracker> Track(List<ReportActor> actors, params Event[] events)
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
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "", 1, null, 0, 20000, null, null, null));

        return parser.StaggerTracker.ShouldNotBeNull();
    }
}
