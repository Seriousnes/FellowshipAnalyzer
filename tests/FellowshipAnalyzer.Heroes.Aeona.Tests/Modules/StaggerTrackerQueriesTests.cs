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

/// <summary>
/// Tests for what <see cref="StaggerTracker"/> serves the segments that rate a cast: the aged Stagger
/// fraction, party alive state, the Stagger removed taken from the report, and the channel bracket.
/// </summary>
public sealed class StaggerTrackerQueriesTests
{
    private const int PlayerId = 310;
    private const int TankId = 90;
    private const int AllyId = 309;
    private const long TankMaxHitPoints = 40_000;

    private static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
    ];

    [Fact]
    public async Task StaggerFraction_IsGivenInsideTheMaxAge()
    {
        var tracker = await Track(SnapshotHeal(TankId, 1000, rawStagger: 1_600_000));

        tracker.StaggerFractionOfMaxHp(TankId, 1500, StaggerTracker.StaggerMaxAgeMs).ShouldBe(0.4);
    }

    [Fact]
    public async Task StaggerFraction_IsWithheldPastTheMaxAge()
    {
        var tracker = await Track(SnapshotHeal(TankId, 1000, rawStagger: 1_600_000));

        tracker.StaggerFractionOfMaxHp(TankId, 2500, StaggerTracker.StaggerMaxAgeMs).ShouldBeNull();
    }

    [Fact]
    public async Task IsAlive_IsTrueForAUnitThatNeverDied()
    {
        var tracker = await Track(SnapshotHeal(TankId, 1000, rawStagger: 100_000));

        tracker.IsAlive(TankId, 5000).ShouldBeTrue();
    }

    [Fact]
    public async Task IsAlive_IsFalseAfterADeathUntilTheUnitIsSeenAlive()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 1000, rawStagger: 100_000),
            Death(TankId, 2000),
            SnapshotHeal(TankId, 6000, rawStagger: 50_000, hitPoints: 12_000));

        tracker.IsAlive(TankId, 1500).ShouldBeTrue();
        tracker.IsAlive(TankId, 3000).ShouldBeFalse();
        tracker.IsAlive(TankId, 6000).ShouldBeTrue();
    }

    [Fact]
    public async Task StaggerRemoved_TakesTheMedianOfTheCleanCastsThatClearedTheirWholeAmount()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 900, rawStagger: 800_000),
            AmendFateCast(1000),
            AmendFateHeal(TankId, 1100, rawStagger: 620_000),

            SnapshotHeal(TankId, 2900, rawStagger: 900_000),
            AmendFateCast(3000),
            AmendFateHeal(TankId, 3100, rawStagger: 700_000),

            SnapshotHeal(TankId, 4900, rawStagger: 700_000),
            AmendFateCast(5000),
            AmendFateHeal(TankId, 5100, rawStagger: 480_000));

        tracker.StaggerRemoved(Spells.AmendFate.FSLID).ShouldBe(2000);
    }

    [Fact]
    public async Task StaggerRemoved_IgnoresACastThatEmptiedThePool()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 900, rawStagger: 150_000),
            AmendFateCast(1000),
            AmendFateHeal(TankId, 1100, rawStagger: 0),

            SnapshotHeal(TankId, 2900, rawStagger: 900_000),
            AmendFateCast(3000),
            AmendFateHeal(TankId, 3100, rawStagger: 700_000));

        tracker.StaggerRemoved(Spells.AmendFate.FSLID).ShouldBe(2000);
    }

    [Fact]
    public async Task StaggerRemoved_IsWithheldWhenNoCastCanBeMeasured()
    {
        var tracker = await Track(AmendFateCast(1000));

        tracker.StaggerRemoved(Spells.AmendFate.FSLID).ShouldBeNull();
    }

    [Fact]
    public async Task MeasureCleanseBetween_BracketsTheWholeWindow()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 900, rawStagger: 800_000),
            SnapshotHeal(TankId, 1500, rawStagger: 700_000),
            SnapshotHeal(TankId, 3100, rawStagger: 400_000));

        var cleanse = tracker.MeasureCleanseBetween(TankId, 1000, 3000, 500).ShouldNotBeNull();

        cleanse.PreAmount.ShouldBe(8000);
        cleanse.PostAmount.ShouldBe(4000);
        cleanse.ClearedAmount.ShouldBe(4000);
    }

    [Fact]
    public async Task MeasureCleanseBetween_ReportsTheCleansesInsideTheWindow()
    {
        var tracker = await Track(
            SnapshotHeal(TankId, 900, rawStagger: 800_000),
            AmendFateCast(1500),
            SnapshotHeal(TankId, 3100, rawStagger: 400_000));

        var cleanse = tracker.MeasureCleanseBetween(TankId, 1000, 3000, 500).ShouldNotBeNull();

        cleanse.InterveningCleanseCount.ShouldBe(1);
        cleanse.HasInterveningEvent.ShouldBeTrue();
    }

    [Fact]
    public async Task MeasureCleanseBetween_IsWithheldWhenNothingClosesTheBracket()
    {
        var tracker = await Track(SnapshotHeal(TankId, 900, rawStagger: 800_000));

        tracker.MeasureCleanseBetween(TankId, 1000, 3000, 500).ShouldBeNull();
    }

    private static ActorResources Resources(int rawStagger, long hitPoints) => new()
    {
        HitPoints = hitPoints,
        MaxHitPoints = TankMaxHitPoints,
        Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = rawStagger, Max = -100 }],
    };

    private static HealEvent SnapshotHeal(int unitId, int timestamp, int rawStagger, long hitPoints = 20_000) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = Resources(rawStagger, hitPoints),
    };

    private static CastEvent AmendFateCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { Id = Spells.AmendFate.FSLID },
    };

    private static HealEvent AmendFateHeal(int unitId, int timestamp, int rawStagger) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = unitId,
        Amount = 5000,
        Ability = new Ability { Id = Spells.AmendFate.FSLID },
        TargetResources = Resources(rawStagger, 20_000),
    };

    private static DeathEvent Death(int unitId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = unitId,
        TargetId = unitId,
    };

    private static async Task<StaggerTracker> Track(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        parser.Actors = Actors;
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "", 1, null, 0, 20_000, null, null, null));

        return parser.StaggerTracker.ShouldNotBeNull();
    }
}
