using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

/// <summary>
/// Exercises the Winter Orb reconstruction against hand-fabricated cast snapshots. The captured
/// Rime fixture carries no resource snapshots at all, so every branch of the reconstruction can
/// only be reached from fabricated events.
/// </summary>
public sealed class WinterOrbTrackerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;
    private const int MaxOrbs = 5;

    private static readonly ReportFight BossFight = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 120_000, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task FirstSnapshot_SeedsThePoolAndIsNotCountedAsGeneration()
    {
        var tracker = await Analyze(
            Cast(1_000, Spells.FrostBolt, orbs: 3));

        tracker.Current.ShouldBe(3);
        tracker.CurrentOrbs.ShouldBe(3);
        tracker.Generated.ShouldBe(0);
        tracker.Spent.ShouldBe(0);
        tracker.Wasted.ShouldBe(0);
        tracker.CappedMs.ShouldBe(0);
        tracker.OvercapIncidents.ShouldBeEmpty();
        ShouldStayInRange(tracker);
    }

    [Fact]
    public async Task RisingSnapshots_CountTheDeltaAsGenerated()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 1),
            Cast(2_000, Spells.FrostBolt, orbs: 2),
            Cast(3_000, Spells.FreezingTorrent, orbs: 4));

        tracker.Generated.ShouldBe(4);
        tracker.Spent.ShouldBe(0);
        tracker.Wasted.ShouldBe(0);
        tracker.Current.ShouldBe(4);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task FallingSnapshots_CountTheDeltaAsSpent()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 4),
            Cast(2_000, Spells.GlacialBlast, orbs: 2),
            Cast(3_000, Spells.GlacialBlast, orbs: 0));

        tracker.Generated.ShouldBe(4);
        tracker.Spent.ShouldBe(4);
        tracker.Wasted.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task GeneratorCastAtCap_CountsOvercapAndRecordsTheIncident()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(2_000, Spells.FrostBolt, orbs: 5));

        tracker.Wasted.ShouldBe(1);
        tracker.Generated.ShouldBe(6);
        tracker.Spent.ShouldBe(0);
        tracker.Current.ShouldBe(MaxOrbs);

        var incident = tracker.OvercapIncidents.ShouldHaveSingleItem();
        incident.AbilityId.ShouldBe(Spells.FrostBolt.Id);
        incident.Timestamp.ShouldBe(2_000);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task ColdSnapAtCap_RecordsTheIncidentAgainstColdSnap()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.BurstingIce, orbs: 5),
            Cast(2_000, Spells.ColdSnap, orbs: 5));

        tracker.OvercapIncidents.ShouldHaveSingleItem().AbilityId.ShouldBe(Spells.ColdSnap.Id);
        tracker.Wasted.ShouldBe(1);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task EveryOrbGenerator_IsAttributedSeparatelyWhenItOvercaps()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(2_000, Spells.FrostBolt, orbs: 5),
            Cast(3_000, Spells.ColdSnap, orbs: 5),
            Cast(4_000, Spells.FreezingTorrent, orbs: 5),
            Cast(5_000, Spells.BurstingIce, orbs: 5));

        tracker.Wasted.ShouldBe(4);
        tracker.OvercapIncidents.Select(incident => incident.AbilityId).ShouldBe(
        [
            Spells.FrostBolt.Id,
            Spells.ColdSnap.Id,
            Spells.FreezingTorrent.Id,
            Spells.BurstingIce.Id,
        ]);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task NonGeneratorCastAtCap_DoesNotCountOvercap()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(2_000, Spells.GlacialBlast, orbs: 5),
            Cast(3_000, Spells.IceComet, orbs: 5));

        tracker.Wasted.ShouldBe(0);
        tracker.Generated.ShouldBe(5);
        tracker.OvercapIncidents.ShouldBeEmpty();
        tracker.CappedMs.ShouldBe(2_000);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task OvercapIncidentCount_AlwaysMatchesWasted()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 3),
            Cast(2_000, Spells.FrostBolt, orbs: 5),
            Cast(3_000, Spells.FreezingTorrent, orbs: 5),
            Cast(4_000, Spells.GlacialBlast, orbs: 3),
            Cast(5_000, Spells.ColdSnap, orbs: 5),
            Cast(6_000, Spells.ColdSnap, orbs: 5));

        tracker.Wasted.ShouldBe(2);
        tracker.OvercapIncidents.Count.ShouldBe(tracker.Wasted);
        tracker.Generated.ShouldBe(9);
        tracker.Spent.ShouldBe(2);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task CappedMs_AccumulatesAcrossASpanThatStartsAndEndsAtTheCap()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(3_500, Spells.FrostBolt, orbs: 5));

        tracker.CappedMs.ShouldBe(2_500);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task CappedMs_DoesNotAccumulateAcrossASpanThatDropsBelowTheCap()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(3_500, Spells.GlacialBlast, orbs: 4));

        tracker.CappedMs.ShouldBe(0);
        tracker.Spent.ShouldBe(1);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task CappedMs_DoesNotAccumulateAcrossASpanThatClimbsToTheCap()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 4),
            Cast(3_500, Spells.FrostBolt, orbs: 5));

        tracker.CappedMs.ShouldBe(0);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task CappedMs_IsMeasuredFromTheSeedingSnapshotNotTheFightStart()
    {
        var tracker = await Analyze(
            Cast(60_000, Spells.FrostBolt, orbs: 5),
            Cast(61_000, Spells.FrostBolt, orbs: 5));

        tracker.CappedMs.ShouldBe(1_000);
        tracker.Wasted.ShouldBe(1);
        ShouldStayInRange(tracker);
    }

    [Fact]
    public async Task CappedMs_SumsEveryCappedSpanAcrossTheFight()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 5),
            Cast(2_000, Spells.FrostBolt, orbs: 5),
            Cast(3_000, Spells.GlacialBlast, orbs: 3),
            Cast(9_000, Spells.FrostBolt, orbs: 5),
            Cast(12_000, Spells.ColdSnap, orbs: 5));

        tracker.CappedMs.ShouldBe(4_000);
        tracker.Wasted.ShouldBe(2);
        tracker.Generated.ShouldBe(9);
        tracker.Spent.ShouldBe(2);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task SnapshotAboveTheCap_IsClampedToTheMaximum()
    {
        var tracker = await Analyze(
            EmptyPool(500),
            Cast(1_000, Spells.FrostBolt, orbs: 2),
            Cast(2_000, Spells.FrostBolt, orbs: 7));

        tracker.Current.ShouldBe(MaxOrbs);
        tracker.Generated.ShouldBe(5);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task MixedGenerationAndSpending_HoldsTheAccountingIdentity()
    {
        var tracker = await Analyze(
            Cast(1_000, Spells.FrostBolt, orbs: 0),
            Cast(2_000, Spells.FrostBolt, orbs: 2),
            Cast(3_000, Spells.GlacialBlast, orbs: 0),
            Cast(4_000, Spells.FreezingTorrent, orbs: 5),
            Cast(5_000, Spells.FreezingTorrent, orbs: 5),
            Cast(6_000, Spells.IceComet, orbs: 3),
            Cast(7_000, Spells.TalonStrike, orbs: 1),
            Cast(8_000, Spells.BurstingIce, orbs: 4));

        tracker.Generated.ShouldBe(11);
        tracker.Spent.ShouldBe(6);
        tracker.Wasted.ShouldBe(1);
        tracker.Current.ShouldBe(4);
        tracker.CappedMs.ShouldBe(1_000);
        ShouldBalance(tracker);
    }

    [Fact]
    public async Task Tracker_PublishesTheWinterOrbCapAndDisplayName()
    {
        var tracker = await Analyze(
            Cast(1_000, Spells.FrostBolt, orbs: 3));

        tracker.MaxOrbs.ShouldBe(MaxOrbs);
        tracker.GetMax(ResourceTypes.Tertiary).ShouldBe(MaxOrbs);
        tracker.GetDisplayName(ResourceTypes.Tertiary).ShouldBe("Winter Orbs");
    }

    [Fact]
    public async Task CastsWithoutAnOrbSnapshot_LeaveTheTrackerUntouched()
    {
        var tracker = await Analyze(
            Cast(1_000, Spells.FrostBolt, orbs: null),
            Cast(2_000, Spells.GlacialBlast, orbs: null),
            Cast(60_000, Spells.ColdSnap, orbs: null));

        tracker.Generated.ShouldBe(0);
        tracker.Spent.ShouldBe(0);
        tracker.Wasted.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.CappedMs.ShouldBe(0);
        tracker.OvercapIncidents.ShouldBeEmpty();
        ShouldBalance(tracker);
    }

    /// <summary>
    /// Asserts the accounting identity, which holds for a pool observed from empty. A scenario that
    /// seeds on a non-empty snapshot carries that seed as untracked Current and uses
    /// <see cref="ShouldStayInRange"/> instead.
    /// </summary>
    private static void ShouldBalance(WinterOrbTracker tracker)
    {
        ShouldStayInRange(tracker);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Wasted + tracker.Current);
    }

    private static void ShouldStayInRange(WinterOrbTracker tracker)
    {
        tracker.Current.ShouldBeInRange(0, tracker.MaxOrbs);
        tracker.CappedMs.ShouldBeGreaterThanOrEqualTo(0);
        tracker.OvercapIncidents.Count.ShouldBe(tracker.Wasted);
    }

    private static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId };

    private static CastEvent EmptyPool(int timestamp) => Cast(timestamp, Spells.FrostBolt, orbs: 0);

    /// <summary>
    /// A player cast, optionally carrying the Winter Orb snapshot the log would attach. Raw
    /// Fellowship resource values are scaled by 100, so the in-game orb count is stored multiplied.
    /// </summary>
    private static CastEvent Cast(int timestamp, Spell spell, int? orbs)
    {
        var cast = new CastEvent
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = EnemyId,
            TargetInstance = 1,
            Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        };

        if (orbs is not null)
        {
            cast.SourceResources = new ActorResources
            {
                Resources = [new ClassResource { Type = ResourceTypes.Tertiary, Amount = orbs.Value * 100, Max = 500 }],
            };
        }

        return cast;
    }

    private static async Task<WinterOrbTracker> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([Combatant(), .. events], PlayerId, BossFight);
        return parser.WinterOrbTracker!;
    }
}
