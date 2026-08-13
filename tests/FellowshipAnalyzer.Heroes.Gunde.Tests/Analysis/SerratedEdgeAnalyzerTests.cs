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

        analyzer.JudgedGrants.ShouldBe(1);
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
        grant.HeartSplitterReady.ShouldBeTrue();
        grant.GrimCarveReady.ShouldBeTrue();
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
        grant.HeartSplitterReady.ShouldBeFalse();
        grant.GrimCarveReady.ShouldBeFalse();
        grant.HeartSplitterRemainingMs.ShouldBeGreaterThan(0);
        grant.GrimCarveRemainingMs.ShouldBeGreaterThan(0);
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
        grant.HeartSplitterReady.ShouldBeFalse();
        grant.GrimCarveReady.ShouldBeTrue();
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
        grant.HeartSplitterReady.ShouldBeTrue();
        grant.GrimCarveReady.ShouldBeTrue();
        grant.Outcome.ShouldBe(SerratedEdgeOutcome.AvoidableFiller);
    }

    [Fact]
    public async Task Analyze_RuptureConsumingTheBuff_IsJudgedAsFiller()
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
    public async Task Analyze_BuffStillUpWhenThePullEnds_IsNotJudged()
    {
        var analyzer = await AnalyzeAsync([Granted(1_000)], boss: true);

        analyzer.Grants.ShouldBeEmpty();
        analyzer.JudgedGrants.ShouldBe(0);
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
        grant.HeartSplitterRemainingMs.ShouldBeLessThanOrEqualTo(HeartSplitterCooldownMs);
        grant.GrimCarveRemainingMs.ShouldBeLessThanOrEqualTo(GrimCarveCooldownMs);
        grant.GrimCarveRemainingMs.ShouldBeGreaterThan(grant.HeartSplitterRemainingMs);
    }

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

    private static async Task<SerratedEdgeAnalyzer> AnalyzeAsync(List<Event> events, bool boss)
    {
        var (parser, _) = await RunAsync(events, boss ? BossDungeon() : TrashDungeon());
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
