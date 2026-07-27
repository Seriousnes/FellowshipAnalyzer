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

public sealed class HeartSplitterAnalyzerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int OtherEnemy = 100;
    private const int PullEnd = 120_000;

    [Fact]
    public async Task Analyze_CastOnABleedingTarget_ReadsTheRendActiveOnIt()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 90),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
            Exsanguinate(5_100, 12_000),
        ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.RendOnTarget.ShouldBe(90);
        cast.ExsanguinateDamage.ShouldBe(12_000);
        cast.MillisecondsSinceSlaughter.ShouldBeNull();
        cast.ClippedBySlaughter.ShouldBeFalse();

        analyzer.ClippedBySlaughter.ShouldBe(0);
        analyzer.TotalRendExsanguinated.ShouldBe(90);
        analyzer.TotalExsanguinateDamage.ShouldBe(12_000);
    }

    [Fact]
    public async Task Analyze_CastRightAfterASlaughterEmptiedTheTarget_IsClipped()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 120),
            Cast(Spells.Slaughter.FSLID, 5_000, BossId),
            RendRemoved(5_010, BossId),
            RendApplied(5_500, BossId),
            RendStack(5_600, BossId, 6),
            Cast(Spells.HeartSplitter.FSLID, 6_000, BossId),
            Exsanguinate(6_100, 900),
        ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.RendOnTarget.ShouldBe(6);
        cast.MillisecondsSinceSlaughter.ShouldBe(1_000);
        cast.ClippedBySlaughter.ShouldBeTrue();

        analyzer.ClippedBySlaughter.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CastLongAfterTheLastSlaughter_IsNotClipped()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.Slaughter.FSLID, 5_000, BossId),
            RendApplied(6_000, BossId),
            RendStack(7_000, BossId, 75),
            Cast(Spells.HeartSplitter.FSLID, 5_001 + HeartSplitterAnalyzer.SlaughterClipMs, BossId),
            Exsanguinate(5_101 + HeartSplitterAnalyzer.SlaughterClipMs, 9_000),
        ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.RendOnTarget.ShouldBe(75);
        cast.ClippedBySlaughter.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_CastExactlyAtTheClipBoundary_IsStillClipped()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.Slaughter.FSLID, 5_000, BossId),
            Cast(Spells.HeartSplitter.FSLID, 5_000 + HeartSplitterAnalyzer.SlaughterClipMs, BossId),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().ClippedBySlaughter.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_TwoEnemiesBleeding_ReadsOnlyTheTargetItNamed()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(1_100, BossId, 200),
            RendApplied(1_200, OtherEnemy),
            RendStack(1_300, OtherEnemy, 12),
            Cast(Spells.HeartSplitter.FSLID, 5_000, OtherEnemy),
            Exsanguinate(5_100, 1_500, OtherEnemy),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().RendOnTarget.ShouldBe(12);
    }

    [Fact]
    public async Task Analyze_TwoInstancesOfOneEnemy_AreTrackedSeparately()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId, targetInstance: 0),
            RendStack(1_100, BossId, 40, targetInstance: 0),
            RendApplied(1_200, BossId, targetInstance: 1),
            RendStack(1_300, BossId, 130, targetInstance: 1),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId, targetInstance: 1),
            Exsanguinate(5_100, 16_000, targetInstance: 1),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().RendOnTarget.ShouldBe(130);
    }

    [Fact]
    public async Task Analyze_ExsanguinateDamage_IsAttributedToTheCastThatProducedIt()
    {
        var analyzer = await AnalyzeAsync(
        [
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
            Exsanguinate(5_100, 4_000),
            Cast(Spells.HeartSplitter.FSLID, 20_000, BossId),
            Exsanguinate(20_100, 9_000),
        ]);

        analyzer.Casts.Count.ShouldBe(2);
        analyzer.Casts[0].ExsanguinateDamage.ShouldBe(4_000);
        analyzer.Casts[1].ExsanguinateDamage.ShouldBe(9_000);
        analyzer.TotalExsanguinateDamage.ShouldBe(13_000);
    }

    [Fact]
    public async Task Analyze_RendPartiallyDecaying_TracksTheAbsoluteCount()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 80),
            RendStackRemoved(3_000, BossId, 55),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
            Exsanguinate(5_100, 7_000),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().RendOnTarget.ShouldBe(55);
    }

    [Fact]
    public async Task Analyze_RendAccruingBetweenTheCastAndTheHit_IsReadAtTheHit()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
            RendStack(5_100, BossId, 44),
            Exsanguinate(5_350, 6_000),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().RendOnTarget.ShouldBe(44);
    }

    [Fact]
    public async Task Analyze_CastWithNoExsanguinateFollowing_LeavesTheBleedUnmeasured()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 70),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
        ]);

        analyzer.Casts.ShouldHaveSingleItem().RendOnTarget.ShouldBeNull();
        analyzer.MeasuredCasts.ShouldBe(0);
        analyzer.TotalRendExsanguinated.ShouldBe(0);
        analyzer.AverageRendOnTarget.ShouldBe(0d);
    }

    [Fact]
    public async Task Analyze_StrikeSpreadAcrossThePack_ReadsTheFirstEnemyAndSumsTheDamage()
    {
        var analyzer = await AnalyzeAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(1_100, BossId, 95),
            RendApplied(1_200, OtherEnemy),
            RendStack(1_300, OtherEnemy, 30),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
            Exsanguinate(5_100, 12_000),
            Exsanguinate(5_100, 4_000, OtherEnemy),
        ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.RendOnTarget.ShouldBe(95);
        cast.ExsanguinateDamage.ShouldBe(16_000);
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([Cast(Spells.HeartSplitter.FSLID, 1_000, BossId)], BossFight());

        var entry = parser.HeartSplitterAnalyzers.ShouldHaveSingleItem();
        entry.Pull.HeartSplitterAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).HeartSplitterAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_TracksRendAcrossTheWholeFight()
    {
        var (parser, _) = await RunAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 64),
            Cast(Spells.HeartSplitter.FSLID, 5_000, BossId),
        ], BossFight());

        var tracker = parser.RendStackTracker.ShouldNotBeNull();
        tracker.StacksOn(BossId, 0).ShouldBe(64);
        tracker.TotalStacks.ShouldBe(64);
        tracker.BleedingTargets.ShouldBe(1);
        tracker.Removals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_RendRemoval_RecordsTheStacksThatWereActive()
    {
        var (parser, _) = await RunAsync(
        [
            RendApplied(1_000, BossId),
            RendStack(2_000, BossId, 64),
            RendRemoved(5_000, BossId),
        ], BossFight());

        var tracker = parser.RendStackTracker.ShouldNotBeNull();
        var removal = tracker.Removals.ShouldHaveSingleItem();
        removal.Timestamp.ShouldBe(5_000);
        removal.Stacks.ShouldBe(64);
        removal.Target.ShouldBe(new RendTarget(BossId, 0));

        tracker.TotalStacks.ShouldBe(0);
        tracker.StacksOn(BossId, 0).ShouldBe(0);
    }

    private static ApplyDebuffEvent RendApplied(int timestamp, int targetId, int? targetInstance = null) =>
        Debuff<ApplyDebuffEvent>(timestamp, targetId, targetInstance);

    private static RemoveDebuffEvent RendRemoved(int timestamp, int targetId, int? targetInstance = null) =>
        Debuff<RemoveDebuffEvent>(timestamp, targetId, targetInstance);

    private static ApplyDebuffStackEvent RendStack(int timestamp, int targetId, int stacks, int? targetInstance = null)
    {
        var debuff = Debuff<ApplyDebuffStackEvent>(timestamp, targetId, targetInstance);
        debuff.Stack = stacks;
        return debuff;
    }

    private static RemoveDebuffStackEvent RendStackRemoved(int timestamp, int targetId, int stacks, int? targetInstance = null)
    {
        var debuff = Debuff<RemoveDebuffStackEvent>(timestamp, targetId, targetInstance);
        debuff.Stack = stacks;
        return debuff;
    }

    private static TEvent Debuff<TEvent>(int timestamp, int targetId, int? targetInstance)
        where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.Rend.FSLID },
    };

    private static DamageEvent Exsanguinate(int timestamp, long amount, int targetId = BossId, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Amount = amount,
        Ability = new Ability { Id = Spells.HeartSplitterDotBonusDamage.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp, int targetId, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static ReportFight BossFight() => new(0, "Boss", 1, null, 0, PullEnd, null, null, null);

    private static async Task<HeartSplitterAnalyzer> AnalyzeAsync(List<Event> events)
    {
        var (parser, _) = await RunAsync(events, BossFight());
        return parser.HeartSplitterAnalyzers.ShouldHaveSingleItem().Analyzer;
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
