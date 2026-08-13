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

public sealed class GundeAnalysisEngineTests
{
    private const int Player = 4;
    private const int OpenWoundsFslid = 1_000_000 + 3233;
    private const int Enemy = 100;
    private const int OtherEnemy = 101;
    private const int ThirdEnemy = 102;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync(TrashEvents(), boss: false);

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_TrashPull_ScoresSlaughterAgainstAoe()
    {
        var (parser, _) = await AnalyzeAsync(TrashEvents(), boss: false);

        var analyzer = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SlaughterCasts.ShouldBe(2);
        analyzer.Shape.ShouldBe(GundePullShape.Aoe);
        analyzer.OpenWoundsTimed.ShouldBe(1);
        analyzer.WellExecuted.ShouldBe(1);

        analyzer.Slaughters[0].OpenWoundsActive.ShouldBeTrue();
        analyzer.Slaughters[0].TargetsHit.ShouldBe(2);
        analyzer.Slaughters[0].WellExecuted.ShouldBeTrue();
        analyzer.Slaughters[0].BleedDamage.ShouldBe(600);

        analyzer.Slaughters[1].OpenWoundsActive.ShouldBeFalse();
        analyzer.Slaughters[1].WellExecuted.ShouldBeFalse();
        analyzer.Slaughters[1].BleedDamage.ShouldBe(150);

        analyzer.TotalBleedDamage.ShouldBe(750);
        analyzer.TotalOpenWoundsWindows.ShouldBe(1);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_ScoresSlaughterAgainstOpenWounds()
    {
        var (parser, _) = await AnalyzeAsync(BossEvents(), boss: true);

        var analyzer = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SlaughterCasts.ShouldBe(2);
        analyzer.Shape.ShouldBe(GundePullShape.Boss);
        analyzer.OpenWoundsTimed.ShouldBe(2);
        analyzer.WellExecuted.ShouldBe(2);

        analyzer.Slaughters[0].WellExecuted.ShouldBeTrue();

        analyzer.Slaughters[1].OpenWoundsActive.ShouldBeTrue();
        analyzer.Slaughters[1].WellExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_AttributesSlaughterBleedDamageToTheCastThatProducedIt()
    {
        var events = new List<Event>
        {
            SlaughterDotTick(1_000, 999),
            Cast(2_000, Spells.Slaughter.FSLID),
            SlaughterDotTick(2_300, 100),
            SlaughterDotTick(2_600, 200),
            Cast(6_000, Spells.Slaughter.FSLID),
            SlaughterDotTick(6_300, 500),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true);

        var analyzer = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Slaughters[0].BleedDamage.ShouldBe(300);
        analyzer.Slaughters[1].BleedDamage.ShouldBe(500);
        analyzer.TotalBleedDamage.ShouldBe(800);
    }

    [Fact]
    public async Task Analyze_AttributesTheRendEachSlaughterConsumedToThatCast()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, Spells.Rend.FSLID, Enemy),
            RendStack(1_100, Enemy, 130),
            Debuff<ApplyDebuffEvent>(1_200, Spells.Rend.FSLID, OtherEnemy),
            RendStack(1_300, OtherEnemy, 45),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(2_010, Spells.Rend.FSLID, Enemy),
            Debuff<RemoveDebuffEvent>(2_020, Spells.Rend.FSLID, OtherEnemy),
            Debuff<ApplyDebuffEvent>(3_000, Spells.Rend.FSLID, Enemy),
            RendStack(3_100, Enemy, 20),
            Cast(6_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(6_010, Spells.Rend.FSLID, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true);

        var analyzer = SingleAnalyzer(parser);
        analyzer.Slaughters[0].RendConsumed.ShouldBe(175);
        analyzer.Slaughters[1].RendConsumed.ShouldBe(20);
        analyzer.TotalRendConsumed.ShouldBe(195);
    }

    [Fact]
    public async Task Analyze_RendRemovedLongAfterASlaughter_IsNotAttributedToIt()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, Spells.Rend.FSLID, Enemy),
            RendStack(1_100, Enemy, 60),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(2_001 + SlaughterUsageAnalyzer.RendConsumeGraceMs, Spells.Rend.FSLID, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true);

        SingleAnalyzer(parser).Slaughters.ShouldHaveSingleItem().RendConsumed.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CountsOpenWoundsWindowsThatClosedWithoutASlaughter()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(2_001, OpenWoundsFslid, Enemy),
            Debuff<ApplyDebuffEvent>(35_000, OpenWoundsFslid, OtherEnemy),
            Debuff<ApplyDebuffEvent>(70_000, OpenWoundsFslid, ThirdEnemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 75_000);

        var entry = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem();
        entry.Pull.EndTime.ShouldBe(75_000);

        entry.Analyzer.TotalOpenWoundsWindows.ShouldBe(2);
        entry.Analyzer.WastedOpenWoundsWindows.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_SlaughterConsumingOpenWoundsOnTheSameMillisecond_StillCountsAsInsideTheWindow()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(5_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(5_000, OpenWoundsFslid, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 40_000);

        var analyzer = SingleAnalyzer(parser);
        analyzer.Slaughters.ShouldHaveSingleItem().OpenWoundsActive.ShouldBeTrue();
        analyzer.OpenWoundsTimed.ShouldBe(1);
        analyzer.TotalOpenWoundsWindows.ShouldBe(1);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ReapplyingOpenWoundsOnTheSameEnemy_ClosesTheStaleWindowAtTheNewApply()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Debuff<ApplyDebuffEvent>(10_000, OpenWoundsFslid, Enemy),
            Cast(10_001, Spells.Slaughter.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 40_000);

        var analyzer = SingleAnalyzer(parser);
        analyzer.Slaughters.ShouldHaveSingleItem().OpenWoundsActive.ShouldBeTrue();
        analyzer.TotalOpenWoundsWindows.ShouldBe(2);
        analyzer.WastedOpenWoundsWindows.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_SlaughterOnTheMillisecondOfTheReapply_CashesInTheStaleWindowToo()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Debuff<ApplyDebuffEvent>(10_000, OpenWoundsFslid, Enemy),
            Cast(10_000, Spells.Slaughter.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 40_000);

        var analyzer = SingleAnalyzer(parser);
        analyzer.TotalOpenWoundsWindows.ShouldBe(2);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_OpenWoundsOnTwoInstancesOfTheSameEnemy_KeepsBothWindowsCashedIn()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy, targetInstance: 0),
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy, targetInstance: 1),
            Cast(2_000, Spells.Slaughter.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 40_000);

        var analyzer = SingleAnalyzer(parser);
        analyzer.TotalOpenWoundsWindows.ShouldBe(2);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrashPull_BleedOnTwoInstancesOfTheSameEnemy_CountsBoth()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy, targetInstance: 0),
            Debuff<ApplyDebuffEvent>(2_060, Spells.SlaughterDot.FSLID, Enemy, targetInstance: 1),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: false);

        var slaughter = SingleAnalyzer(parser).Slaughters.ShouldHaveSingleItem();
        slaughter.TargetsHit.ShouldBe(2);
        slaughter.WellExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_TrashPull_OpenWoundsButASingleTarget_IsNotWellExecuted()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: false);

        var analyzer = SingleAnalyzer(parser);
        var slaughter = analyzer.Slaughters.ShouldHaveSingleItem();
        slaughter.OpenWoundsActive.ShouldBeTrue();
        slaughter.TargetsHit.ShouldBe(1);
        slaughter.WellExecuted.ShouldBeFalse();
        analyzer.WellExecuted.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrashPull_PackHitWithoutOpenWounds_IsNotWellExecuted()
    {
        var events = new List<Event>
        {
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
            Debuff<ApplyDebuffEvent>(2_060, Spells.SlaughterDot.FSLID, OtherEnemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: false);

        var analyzer = SingleAnalyzer(parser);
        var slaughter = analyzer.Slaughters.ShouldHaveSingleItem();
        slaughter.OpenWoundsActive.ShouldBeFalse();
        slaughter.TargetsHit.ShouldBe(2);
        slaughter.WellExecuted.ShouldBeFalse();
        analyzer.WellExecuted.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_SlaughterOutsideOpenWounds_IsNotWellExecuted()
    {
        var events = new List<Event>
        {
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true);

        var analyzer = SingleAnalyzer(parser);
        var slaughter = analyzer.Slaughters.ShouldHaveSingleItem();
        slaughter.OpenWoundsActive.ShouldBeFalse();
        slaughter.WellExecuted.ShouldBeFalse();
        analyzer.WellExecuted.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_OpenWoundsRefresh_ExtendsTheWindowRatherThanOpeningANewOne()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Debuff<RefreshDebuffEvent>(15_000, OpenWoundsFslid, Enemy),
            Cast(20_000, Spells.Slaughter.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, dungeonEnd: 40_000);

        var analyzer = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Slaughters.ShouldHaveSingleItem().OpenWoundsActive.ShouldBeTrue();
        analyzer.TotalOpenWoundsWindows.ShouldBe(1);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(TrashEvents(), boss: false);

        var entry = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);
        entry.Analyzer.SlaughterCasts.ShouldBeGreaterThan(0);

        pull.SlaughterUsageAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).SlaughterUsageAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_DungeonPulls_ScoreEachPullOnItsOwnShapeAndReadBackPerPull()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
            Debuff<ApplyDebuffEvent>(2_060, Spells.SlaughterDot.FSLID, OtherEnemy),
            Debuff<ApplyDebuffEvent>(31_000, OpenWoundsFslid, Enemy),
            Cast(32_000, Spells.Slaughter.FSLID),
            Debuff<ApplyDebuffEvent>(32_050, Spells.SlaughterDot.FSLID, Enemy),
        };

        var (parser, _) = await AnalyzeAsync(events, Dungeon());

        parser.SlaughterUsageAnalyzers.Count.ShouldBe(2);
        var trash = parser.SlaughterUsageAnalyzers[0];
        var boss = parser.SlaughterUsageAnalyzers[1];

        trash.Pull.Index.ShouldBe(0);
        trash.Analyzer.Shape.ShouldBe(GundePullShape.Aoe);
        var trashSlaughter = trash.Analyzer.Slaughters.ShouldHaveSingleItem();
        trashSlaughter.TargetsHit.ShouldBe(2);
        trashSlaughter.WellExecuted.ShouldBeTrue();

        boss.Pull.Index.ShouldBe(1);
        boss.Analyzer.Shape.ShouldBe(GundePullShape.Boss);
        var bossSlaughter = boss.Analyzer.Slaughters.ShouldHaveSingleItem();
        bossSlaughter.OpenWoundsActive.ShouldBeTrue();
        bossSlaughter.WellExecuted.ShouldBeTrue();

        trash.Pull.SlaughterUsageAnalyzer.ShouldBeSameAs(trash.Analyzer);
        parser.For(trash.Pull).SlaughterUsageAnalyzer.ShouldBeSameAs(trash.Analyzer);
        boss.Pull.SlaughterUsageAnalyzer.ShouldBeSameAs(boss.Analyzer);
        parser.For(boss.Pull).SlaughterUsageAnalyzer.ShouldBeSameAs(boss.Analyzer);
    }

    [Fact]
    public async Task Analyze_ExposesBloodFeatherTrackerAndAuras()
    {
        var (parser, _) = await AnalyzeAsync(BloodFeatherEvents(), boss: true);

        parser.GundeAuras.ShouldNotBeNull();
        parser.GundeAuras.TimelineHighlightedIds.ShouldContain(Spells.ReignInBloodSelfBuff.FSLID.Value);

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.Generated.ShouldBe(42);
        tracker.Spent.ShouldBe(42);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.CappedMs.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    private static List<Event> BloodFeatherEvents() =>
    [
        FeatherBuff<ApplyBuffEvent>(1_000),
        FeatherStack(2_000, 42),
        Cast(5_000, Spells.OwedInBlood.FSLID),
        FeatherBuff<RemoveBuffEvent>(5_100),
    ];

    private static TEvent FeatherBuff<TEvent>(int timestamp) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = Player,
        Ability = new Ability { FSLID = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static ApplyBuffStackEvent FeatherStack(int timestamp, int stacks) => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = Player,
        Stack = stacks,
        Ability = new Ability { FSLID = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static SlaughterUsageAnalyzer SingleAnalyzer(GundeCombatLogParser parser) =>
        parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;

    private static ReportDungeon Dungeon() =>
        new(0, "Dungeon", 0, true, 0, 60_000, null, null, null, false,
            [
                new DungeonPull(1, 0, null, 0, 20_000, "Trash", null),
                new DungeonPull(2, 42, true, 30_000, 60_000, "Boss", null),
            ]);

    private static Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        List<Event> events, bool boss, int dungeonEnd = 10_000) =>
        AnalyzeAsync(events, new ReportDungeon(0, "Test", boss ? 1 : 0, true, 0, dungeonEnd, null, null, null));

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
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
        var result = await parser.Analyze(events, Player, dungeon);
        return (parser, result);
    }

    private static List<Event> TrashEvents() =>
    [
        Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
        Cast(1_500, Spells.HeartSplitter.FSLID),
        Cast(2_000, Spells.Slaughter.FSLID),
        Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
        Debuff<ApplyDebuffEvent>(2_060, Spells.SlaughterDot.FSLID, OtherEnemy),
        SlaughterDotTick(2_100, 400),
        SlaughterDotTick(2_400, 200),
        Debuff<RemoveDebuffEvent>(3_000, OpenWoundsFslid, Enemy),
        Cast(5_000, Spells.Slaughter.FSLID),
        Debuff<ApplyDebuffEvent>(5_050, Spells.SlaughterDot.FSLID, Enemy),
        SlaughterDotTick(5_100, 150),
    ];

    private static List<Event> BossEvents() =>
    [
        Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
        Cast(2_000, Spells.Slaughter.FSLID),
        Debuff<ApplyDebuffEvent>(2_050, Spells.SlaughterDot.FSLID, Enemy),
        Cast(6_000, Spells.Slaughter.FSLID),
        Debuff<ApplyDebuffEvent>(6_050, Spells.SlaughterDot.FSLID, Enemy),
    ];

    private static CastEvent Cast(int timestamp, int abilityFslid) => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = Enemy,
        Ability = new Ability { FSLID = abilityFslid },
        Target = new CastTarget(),
    };

    private static DamageEvent SlaughterDotTick(int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = Enemy,
        Amount = amount,
        Ability = new Ability { FSLID = Spells.SlaughterDot.FSLID },
    };

    private static ApplyDebuffStackEvent RendStack(int timestamp, int targetId, int stacks)
    {
        var debuff = Debuff<ApplyDebuffStackEvent>(timestamp, Spells.Rend.FSLID, targetId);
        debuff.Stack = stacks;
        return debuff;
    }

    private static TEvent Debuff<TEvent>(int timestamp, int abilityFslid, int targetId, int? targetInstance = null)
        where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = abilityFslid },
    };

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
