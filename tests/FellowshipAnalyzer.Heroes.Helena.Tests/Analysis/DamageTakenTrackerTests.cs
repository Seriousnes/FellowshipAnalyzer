using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class DamageTakenTrackerTests
{
    [Fact]
    public async Task Prevented_IsMitigatedPlusAbsorbedAndExcludesBlocked()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 0, 10_000, 9_000, absorbed: 1_000, blocked: 4_000, hitType: HitType.Block));

        tracker.TotalFaced.ShouldBe(10_000);
        tracker.TotalTaken.ShouldBe(0);
        tracker.TotalPrevented.ShouldBe(10_000);
        tracker.TotalBlocked.ShouldBe(4_000);
        tracker.PreventedShare.ShouldBe(1d);
    }

    [Fact]
    public async Task BlockRate_IsTheShareOfHitsThatArrivedAsABlock()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 100, 1_000, 900),
            DamageTaken(PullStart + 2_000, 0, 1_000, 900, blocked: 100, hitType: HitType.Block),
            DamageTaken(PullStart + 3_000, 0, 1_000, 900, blocked: 100, hitType: HitType.Block),
            DamageTaken(PullStart + 4_000, 100, 1_000, 900));

        tracker.TotalHits.ShouldBe(4);
        tracker.TotalBlockedHits.ShouldBe(2);
        tracker.BlockRate.ShouldBe(0.5);
    }

    [Fact]
    public async Task DamageTakenPerSecond_IsSpreadOverTheFightDuration()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 62_000, 62_000, 0));

        tracker.DamageTakenPerSecond.ShouldBe(1_000, 0.001);
    }

    [Fact]
    public async Task Sources_AreSplitByAbilityAndOrderedByDamageThatLanded()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 100, 1_000, 900),
            DamageTaken(PullStart + 2_000, 100, 1_000, 900),
            Bleed(PullStart + 3_000, 500));

        var sources = tracker.BySource;
        sources.Count.ShouldBe(2);
        sources[0].Taken.ShouldBe(500);
        sources[0].Hits.ShouldBe(1);
        sources[1].Taken.ShouldBe(200);
        sources[1].Hits.ShouldBe(2);
    }

    [Fact]
    public async Task AFightWithNoIncomingDamage_PublishesNoStatisticsCard()
    {
        var tracker = await Analyze(Cast(PullStart + 1_000, Spells.ShieldSlam));

        tracker.TotalTaken.ShouldBe(0);
        tracker.TotalHits.ShouldBe(0);
        tracker.BlockRate.ShouldBe(0);
        tracker.StatisticsComponentType.ShouldBeNull();
    }

    private static DamageEvent Bleed(int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemyId,
        TargetId = PlayerId,
        Ability = new Core.Events.Ability { FSLID = Spells.SweepingStrikeBleedDebuff.FSLID, Name = "Bleed" },
        AbilityGameId = Spells.SweepingStrikeBleedDebuff.FSLID,
        Amount = amount,
        UnmitigatedAmount = amount,
    };

    private static async Task<DamageTakenTracker> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.DamageTakenTracker.ShouldNotBeNull();
    }
}
