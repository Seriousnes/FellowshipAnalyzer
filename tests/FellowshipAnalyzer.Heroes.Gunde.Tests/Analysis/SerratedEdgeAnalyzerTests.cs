using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class SerratedEdgeAnalyzerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int PullEnd = 120_000;

    /// <summary>Curated Heart Splitter cooldown, long enough to outlast a Grim Carve cast next to it.</summary>
    private const int HeartSplitterCooldownMs = 12_000;

    /// <summary>Curated Grim Carve cooldown.</summary>
    private const int GrimCarveCooldownMs = 15_000;

    private static readonly int BleedingHeartRing = FellowshipAnalyzer.Core.Common.Items.Items.BandOfTheBleedingHeart.Id;

    private static readonly int SinisterApron = FellowshipAnalyzer.Core.Common.Items.Items.CarversSinisterApron.Id;

    [Fact]
    public async Task Analyze_BossPull_HeartSplitterConsumingTheBuff_IsThePriority()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.BloodArc.FSLID, 1_000),
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_001),
        ], boss: true);

        analyzer.Shape.ShouldBe(GundePullShape.Boss);
        analyzer.PriorityAbilityId.ShouldBe(Spells.HeartSplitter.FSLID.Value);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.HeartSplitter.FSLID.Value);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Priority);

        analyzer.RatedGrants.ShouldBe(1);
        analyzer.PriorityConsumed.ShouldBe(1);
        analyzer.AlternateConsumed.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_GrimCarveConsumingTheBuff_IsTheAlternate()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
        ], boss: true);

        analyzer.Grants.ShouldHaveSingleItem().Outcome.ShouldBe(SerratedEdgeOutcome.Alternate);
        analyzer.AlternateConsumed.ShouldBe(1);
        analyzer.PriorityConsumed.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrashPull_GrimCarveConsumingTheBuff_IsThePriority()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
        ], boss: false);

        analyzer.Shape.ShouldBe(GundePullShape.Aoe);
        analyzer.PriorityAbilityId.ShouldBe(Spells.GrimCarve.FSLID.Value);
        analyzer.Grants.ShouldHaveSingleItem().Outcome.ShouldBe(SerratedEdgeOutcome.Priority);
    }

    [Fact]
    public async Task Analyze_TrashPull_HeartSplitterConsumingTheBuff_IsTheAlternate()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_000),
        ], boss: false);

        analyzer.Grants.ShouldHaveSingleItem().Outcome.ShouldBe(SerratedEdgeOutcome.Alternate);
        analyzer.AlternateConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FillerWhileABetterAbilityWasReady_IsAvoidable()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.ReaverEdge.FSLID, 2_000),
            Removed(2_001),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.ReaverEdge.FSLID.Value);
        grant.ConsumerRank.ShouldBeNull();
        Readiness(grant, Spells.HeartSplitter.FSLID).Ready.ShouldBeTrue();
        Readiness(grant, Spells.GrimCarve.FSLID).Ready.ShouldBeTrue();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.AvoidableFiller);

        analyzer.AvoidableFiller.ShouldBe(1);
        analyzer.ForcedFiller.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FillerWithBothBetterAbilitiesOnCooldown_IsForced()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Cast(Spells.GrimCarve.FSLID, 1_500),
            Granted(2_000),
            Cast(Spells.ReaverEdge.FSLID, 3_000),
            Removed(3_001),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.ReaverEdge.FSLID.Value);
        Readiness(grant, Spells.HeartSplitter.FSLID).Ready.ShouldBeFalse();
        Readiness(grant, Spells.GrimCarve.FSLID).Ready.ShouldBeFalse();
        Readiness(grant, Spells.HeartSplitter.FSLID).RemainingMs.ShouldBeGreaterThan(0);
        Readiness(grant, Spells.GrimCarve.FSLID).RemainingMs.ShouldBeGreaterThan(0);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.ForcedFiller);

        analyzer.ForcedFiller.ShouldBe(1);
        analyzer.AvoidableFiller.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FillerWithOnlyOneBetterAbilityOnCooldown_IsStillAvoidable()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Granted(2_000),
            Cast(Spells.ReaverEdge.FSLID, 3_000),
            Removed(3_001),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        Readiness(grant, Spells.HeartSplitter.FSLID).Ready.ShouldBeFalse();
        Readiness(grant, Spells.GrimCarve.FSLID).Ready.ShouldBeTrue();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.AvoidableFiller);
    }

    [Fact]
    public async Task Analyze_FillerAfterTheBetterAbilitiesRecharged_IsAvoidableAgain()
    {
        var recovered = 2_000 + GrimCarveCooldownMs + 5_000;
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Granted(recovered),
            Cast(Spells.ReaverEdge.FSLID, recovered + 500),
            Removed(recovered + 501),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        Readiness(grant, Spells.HeartSplitter.FSLID).Ready.ShouldBeTrue();
        Readiness(grant, Spells.GrimCarve.FSLID).Ready.ShouldBeTrue();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.AvoidableFiller);
    }

    [Fact]
    public async Task Analyze_RuptureConsumingTheBuff_IsRatedAsFiller()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Cast(Spells.GrimCarve.FSLID, 1_500),
            Granted(2_000),
            Cast(Spells.Rupture.FSLID, 3_000),
            Removed(3_000),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.Rupture.FSLID.Value);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.ForcedFiller);
    }

    [Fact]
    public async Task Analyze_BossPull_WithTheBleedingHeartRing_PutsRuptureFirst()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.Rupture.FSLID, 2_000),
            Removed(2_000),
        ], boss: true, equipped: BleedingHeartRing);

        analyzer.BleedingHeartRingEquipped.ShouldBeTrue();
        analyzer.ConsumerPriority.ShouldBe(
            [Spells.Rupture.FSLID.Value, Spells.HeartSplitter.FSLID.Value, Spells.GrimCarve.FSLID.Value]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerRank.ShouldBe(0);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Priority);
    }

    [Fact]
    public async Task Analyze_TrashPull_WithTheBleedingHeartRing_KeepsTheSameOrder()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_000),
        ], boss: false, equipped: BleedingHeartRing);

        analyzer.ConsumerPriority.ShouldBe(
            [Spells.Rupture.FSLID.Value, Spells.HeartSplitter.FSLID.Value, Spells.GrimCarve.FSLID.Value]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerRank.ShouldBe(1);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Alternate);
    }

    [Fact]
    public async Task Analyze_BossPull_WithTheSinisterApron_PutsRuptureAheadOfGrimCarve()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
        ], boss: true, equipped: SinisterApron);

        analyzer.SinisterApronEquipped.ShouldBeTrue();
        analyzer.ConsumerPriority.ShouldBe(
            [Spells.Rupture.FSLID.Value, Spells.GrimCarve.FSLID.Value, Spells.HeartSplitter.FSLID.Value]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerRank.ShouldBe(1);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Alternate);
    }

    [Fact]
    public async Task Analyze_TrashPull_WithTheSinisterApron_PutsGrimCarveFirst()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
        ], boss: false, equipped: SinisterApron);

        analyzer.ConsumerPriority.ShouldBe(
            [Spells.GrimCarve.FSLID.Value, Spells.Rupture.FSLID.Value, Spells.HeartSplitter.FSLID.Value]);
        analyzer.PriorityAbilityId.ShouldBe(Spells.GrimCarve.FSLID.Value);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerRank.ShouldBe(0);
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Priority);
    }

    [Fact]
    public async Task Analyze_WithNoLegendary_RuptureIsNotRanked()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.Rupture.FSLID, 2_000),
            Removed(2_000),
        ], boss: true);

        analyzer.BleedingHeartRingEquipped.ShouldBeFalse();
        analyzer.SinisterApronEquipped.ShouldBeFalse();
        analyzer.ConsumerPriority.ShouldBe(
            [Spells.HeartSplitter.FSLID.Value, Spells.GrimCarve.FSLID.Value]);

        analyzer.Grants.ShouldHaveSingleItem().ConsumerRank.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_WithTheBleedingHeartRing_RuptureOffCooldownMakesFillerAvoidable()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Cast(Spells.GrimCarve.FSLID, 1_500),
            Granted(2_000),
            Cast(Spells.ReaverEdge.FSLID, 3_000),
            Removed(3_001),
        ], boss: true, equipped: BleedingHeartRing);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.Readiness.Count.ShouldBe(3);
        Readiness(grant, Spells.Rupture.FSLID).Ready.ShouldBeTrue();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.AvoidableFiller);
    }

    [Fact]
    public async Task Analyze_RemovalWithNoCastToAccountForIt_IsUnspent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Removed(9_000),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBeNull();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Unspent);

        analyzer.Unspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CastFurtherBackThanTheGraceWindow_DoesNotClaimTheRemoval()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_001 + SerratedEdgeAnalyzer.ConsumerGraceMs),
        ], boss: true);

        analyzer.Grants.ShouldHaveSingleItem().Outcome.ShouldBe(SerratedEdgeOutcome.Unspent);
    }

    [Fact]
    public async Task Analyze_CastPrecedingTheGrant_IsNotItsConsumer()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Granted(1_100),
            Removed(1_200),
        ], boss: true);

        analyzer.Grants.ShouldHaveSingleItem().Outcome.ShouldBe(SerratedEdgeOutcome.Unspent);
    }

    [Fact]
    public async Task Analyze_SecondBloodArcConsumingAndRegrantingAtOnce_RecordsBothGrants()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 500),
            Cast(Spells.GrimCarve.FSLID, 800),
            Granted(1_000),
            Cast(Spells.BloodArc.FSLID, 2_000),
            Removed(2_000),
            Granted(2_000),
            Cast(Spells.HeartSplitter.FSLID, 3_000),
            Removed(3_000),
        ], boss: true);

        analyzer.Grants.Count.ShouldBe(2);
        analyzer.Grants[0].ConsumerAbilityId.ShouldBe(Spells.BloodArc.FSLID.Value);
        analyzer.Grants[0].Outcome.ShouldBe(SerratedEdgeOutcome.ForcedFiller);
        analyzer.Grants[1].ConsumerAbilityId.ShouldBe(Spells.HeartSplitter.FSLID.Value);
        analyzer.Grants[1].Outcome.ShouldBe(SerratedEdgeOutcome.Priority);
    }

    [Fact]
    public async Task Analyze_BuffStillUpWhenThePullEnds_IsNotRated()
    {
        var analyzer = await AnalyzeAsync([Granted(1_000)], boss: true);

        analyzer.Grants.ShouldBeEmpty();
        analyzer.RatedGrants.ShouldBe(0);
        analyzer.Unspent.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([Granted(1_000), Removed(2_000)], BossDungeon());

        var entry = parser.SerratedEdgeAnalyzers.ShouldHaveSingleItem();
        entry.Pull.SerratedEdgeAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).SerratedEdgeAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_CooldownRemaining_CountsDownFromTheCuratedCooldown()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Cast(Spells.GrimCarve.FSLID, 1_000),
            Granted(2_000),
            Cast(Spells.ReaverEdge.FSLID, 3_000),
            Removed(3_001),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        var heartSplitter = Readiness(grant, Spells.HeartSplitter.FSLID).RemainingMs;
        var grimCarve = Readiness(grant, Spells.GrimCarve.FSLID).RemainingMs;

        heartSplitter.ShouldBeLessThanOrEqualTo(HeartSplitterCooldownMs);
        grimCarve.ShouldBeLessThanOrEqualTo(GrimCarveCooldownMs);
        grimCarve.ShouldBeGreaterThan(heartSplitter);
    }

    [Fact]
    public async Task Analyze_GrimCarveConsumingTheBuff_ConvertsAFifthOfEverySpin()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
            Damage(Spells.GrimCarve.FSLID, 2_200, 1_000),
            Damage(Spells.GrimCarve.FSLID, 2_700, 1_000),
            Damage(Spells.GrimCarve.FSLID, 3_200, 500),
        ], boss: false);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerDamage.ShouldBe(2_500);
        grant.RendConverted.ShouldBe(500);
        analyzer.TotalRendConverted.ShouldBe(500);
    }

    [Fact]
    public async Task Analyze_HeartSplitterConsumingTheBuff_LeavesExsanguinateOutOfTheConversion()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_000),
            Damage(Spells.HeartSplitter.FSLID, 2_005, 4_000),
            Damage(Spells.HeartSplitterDotBonusDamage.FSLID, 2_006, 16_000),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerDamage.ShouldBe(4_000);
        grant.RendConverted.ShouldBe(800);
        grant.ExsanguinateDamage.ShouldBe(16_000);
    }

    [Fact]
    public async Task Analyze_ASecondCastOfTheSameAbility_KeepsItsDamageOutOfTheFirstGrant()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
            Damage(Spells.GrimCarve.FSLID, 2_100, 1_000),
            Cast(Spells.GrimCarve.FSLID, 2_500),
            Damage(Spells.GrimCarve.FSLID, 2_600, 9_000),
        ], boss: false);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerDamage.ShouldBe(1_000);
        grant.RendConverted.ShouldBe(200);
    }

    [Fact]
    public async Task Analyze_AnotherAbilitysDamage_IsNotConverted()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.DoubleStrike.FSLID, 2_000),
            Removed(2_000),
            Damage(Spells.DoubleStrike.FSLID, 2_010, 400),
            Damage(Spells.DoubleStrike.FSLID, 2_020, 400),
            Damage(Spells.ReaverEdge.FSLID, 2_030, 5_000),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerDamage.ShouldBe(800);
        grant.RendConverted.ShouldBe(160);
    }

    [Fact]
    public async Task Analyze_AnUnspentGrant_ConvertsNothing()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Removed(9_000),
            Damage(Spells.GrimCarve.FSLID, 9_100, 5_000),
        ], boss: true);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.Unspent);
        grant.ConsumerDamage.ShouldBe(0);
        grant.RendConverted.ShouldBe(0);
        analyzer.TotalRendConverted.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_EveryGrant_AddsIntoTheConvertedTotal()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
            Damage(Spells.GrimCarve.FSLID, 2_100, 10_000),
            Granted(20_000),
            Cast(Spells.DoubleStrike.FSLID, 21_000),
            Removed(21_000),
            Damage(Spells.DoubleStrike.FSLID, 21_010, 1_000),
        ], boss: false);

        analyzer.Grants.Count.ShouldBe(2);
        analyzer.TotalRendConverted.ShouldBe(2_200);
        analyzer.RendConvertedBy(SerratedEdgeOutcome.Priority).ShouldBe(2_000);
        analyzer.RendConvertedBy(SerratedEdgeOutcome.AvoidableFiller).ShouldBe(200);
    }

    private static ConsumerReadiness Readiness(SerratedEdgeGrant grant, int abilityId) =>
        grant.Readiness.Single(entry => entry.AbilityId == abilityId);

    private static DamageEvent Damage(int abilityId, int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Amount = amount,
        Ability = new Ability { Id = abilityId },
    };

    private static CombatantInfoEvent Equipped(int itemId) => new()
    {
        SourceId = PlayerId,
        Gear = [new Item { Id = itemId }],
    };

    private static ApplyBuffEvent Granted(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.SerratedEdge.FSLID },
    };

    private static RemoveBuffEvent Removed(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.SerratedEdge.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static ReportDungeon BossDungeon() => new(0, "Boss", 1, null, 0, PullEnd, null, null, null);

    private static ReportDungeon TrashDungeon() =>
        new(0, "Trash", 0, null, 0, PullEnd, null, null, null,
            EnemyNpcs: [new DungeonNpc(1, 100, 4, 1, null)]);

    private static async Task<SerratedEdgeAnalyzer> AnalyzeAsync(List<Event> events, bool boss, int? equipped = null)
    {
        List<Event> stream = equipped is { } itemId ? [Equipped(itemId), .. events] : events;
        var (parser, _) = await RunAsync(stream, boss ? BossDungeon() : TrashDungeon());
        return parser.SerratedEdgeAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> RunAsync(
        List<Event> events, ReportDungeon dungeon)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, dungeon);
        return (parser, result);
    }

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
