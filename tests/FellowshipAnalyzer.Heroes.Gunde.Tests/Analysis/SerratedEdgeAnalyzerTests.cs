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
    private const int PullEnd = 60_000;

    [Fact]
    public async Task Analyze_HeartSplitterConsumingTheBuff_IsWellSpent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.BloodArc.FSLID, 1_000),
            Granted(1_000),
            Cast(Spells.HeartSplitter.FSLID, 2_000),
            Removed(2_001),
        ]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.HeartSplitter.FSLID.Value);
        grant.WellSpent.ShouldBeTrue();
        grant.HeldMs.ShouldBe(1_001);

        analyzer.JudgedGrants.ShouldBe(1);
        analyzer.WellSpent.ShouldBe(1);
        analyzer.Misspent.ShouldBe(0);
        analyzer.Unspent.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_GrimCarveConsumingTheBuff_IsWellSpent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.GrimCarve.FSLID, 2_000),
            Removed(2_000),
        ]);

        analyzer.Grants.ShouldHaveSingleItem().WellSpent.ShouldBeTrue();
        analyzer.WellSpent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FillerConsumingTheBuff_IsMisspent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.ReaverEdge.FSLID, 2_000),
            Removed(2_001),
        ]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBe(Spells.ReaverEdge.FSLID.Value);
        grant.WellSpent.ShouldBeFalse();

        analyzer.WellSpent.ShouldBe(0);
        analyzer.Misspent.ShouldBe(1);
        analyzer.Unspent.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RuptureConsumingTheBuff_IsMisspent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.Rupture.FSLID, 2_000),
            Removed(2_000),
        ]);

        analyzer.Grants.ShouldHaveSingleItem().ConsumerAbilityId.ShouldBe(Spells.Rupture.FSLID.Value);
        analyzer.Misspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_RemovalWithNoCastToAccountForIt_IsUnspent()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Removed(9_000),
        ]);

        var grant = analyzer.Grants.ShouldHaveSingleItem();
        grant.ConsumerAbilityId.ShouldBeNull();
        grant.WellSpent.ShouldBeFalse();

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
        ]);

        analyzer.Grants.ShouldHaveSingleItem().ConsumerAbilityId.ShouldBeNull();
        analyzer.Unspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CastPrecedingTheGrant_IsNotItsConsumer()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 1_000),
            Granted(1_100),
            Removed(1_200),
        ]);

        analyzer.Grants.ShouldHaveSingleItem().ConsumerAbilityId.ShouldBeNull();
        analyzer.Unspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_SecondBloodArcConsumingAndRegrantingAtOnce_RecordsBothGrants()
    {
        var analyzer = await AnalyzeAsync(
        [
            Granted(1_000),
            Cast(Spells.BloodArc.FSLID, 2_000),
            Removed(2_000),
            Granted(2_000),
            Cast(Spells.GrimCarve.FSLID, 3_000),
            Removed(3_000),
        ]);

        analyzer.Grants.Count.ShouldBe(2);
        analyzer.Grants[0].ConsumerAbilityId.ShouldBe(Spells.BloodArc.FSLID.Value);
        analyzer.Grants[1].ConsumerAbilityId.ShouldBe(Spells.GrimCarve.FSLID.Value);

        analyzer.WellSpent.ShouldBe(1);
        analyzer.Misspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_BuffStillUpWhenThePullEnds_IsNotJudged()
    {
        var analyzer = await AnalyzeAsync([Granted(1_000)]);

        analyzer.Grants.ShouldBeEmpty();
        analyzer.JudgedGrants.ShouldBe(0);
        analyzer.Unspent.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([Granted(1_000), Removed(2_000)], BossFight());

        var entry = parser.SerratedEdgeAnalyzers.ShouldHaveSingleItem();
        entry.Pull.SerratedEdgeAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).SerratedEdgeAnalyzer.ShouldBeSameAs(entry.Analyzer);
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

    private static ReportFight BossFight() => new(0, "Boss", 1, null, 0, PullEnd, null, null, null);

    private static async Task<SerratedEdgeAnalyzer> AnalyzeAsync(List<Event> events)
    {
        var (parser, _) = await RunAsync(events, BossFight());
        return parser.SerratedEdgeAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> RunAsync(
        List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
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
