using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
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
        analyzer.Slaughters[0].PayoffDamage.ShouldBe(600);

        analyzer.Slaughters[1].OpenWoundsActive.ShouldBeFalse();
        analyzer.Slaughters[1].WellExecuted.ShouldBeFalse();
        analyzer.Slaughters[1].PayoffDamage.ShouldBe(150);

        analyzer.BestPayoff.ShouldBe(600);
        analyzer.TotalPayoffDamage.ShouldBe(750);
        analyzer.TotalOpenWoundsWindows.ShouldBe(1);
        analyzer.WastedOpenWoundsWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BossPull_ScoresSlaughterAgainstHeartSplitterPriming()
    {
        var (parser, _) = await AnalyzeAsync(BossEvents(), boss: true);

        var analyzer = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SlaughterCasts.ShouldBe(2);
        analyzer.Shape.ShouldBe(GundePullShape.Boss);
        analyzer.OpenWoundsTimed.ShouldBe(2);
        analyzer.HeartSplitterPrimed.ShouldBe(1);
        analyzer.WellExecuted.ShouldBe(1);

        analyzer.Slaughters[0].WellExecuted.ShouldBeTrue();

        analyzer.Slaughters[1].OpenWoundsActive.ShouldBeTrue();
        analyzer.Slaughters[1].HeartSplitterPrimed.ShouldBeFalse();
        analyzer.Slaughters[1].WellExecuted.ShouldBeFalse();
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
        analyzer.Slaughters[0].PayoffDamage.ShouldBe(300);
        analyzer.Slaughters[1].PayoffDamage.ShouldBe(500);
        analyzer.TotalPayoffDamage.ShouldBe(800);
        analyzer.BestPayoff.ShouldBe(500);
    }

    [Fact]
    public async Task Analyze_CountsOpenWoundsWindowsThatClosedWithoutASlaughter()
    {
        var events = new List<Event>
        {
            Debuff<ApplyDebuffEvent>(1_000, OpenWoundsFslid, Enemy),
            Cast(2_000, Spells.Slaughter.FSLID),
            Debuff<RemoveDebuffEvent>(3_000, OpenWoundsFslid, Enemy),
            Debuff<ApplyDebuffEvent>(5_000, OpenWoundsFslid, OtherEnemy),
            Debuff<ApplyDebuffEvent>(35_000, OpenWoundsFslid, ThirdEnemy),
        };

        var (parser, _) = await AnalyzeAsync(events, boss: true, fightEnd: 40_000);

        var entry = parser.SlaughterUsageAnalyzers.ShouldHaveSingleItem();
        entry.Pull.EndTime.ShouldBe(40_000);

        entry.Analyzer.TotalOpenWoundsWindows.ShouldBe(2);
        entry.Analyzer.WastedOpenWoundsWindows.ShouldBe(1);
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

        var (parser, _) = await AnalyzeAsync(events, boss: true, fightEnd: 40_000);

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
    public async Task Analyze_ExposesBloodFeatherTrackerAndAuras()
    {
        var (parser, _) = await AnalyzeAsync(BloodFeatherEvents(), boss: true);

        parser.GundeAuras.ShouldNotBeNull();
        parser.GundeAuras.TimelineHighlightedIds.ShouldContain(Spells.ReignInBloodSelfBuff.FSLID.Value);

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.GetDisplayName(ResourceTypes.Tertiary).ShouldBe("Blood Feathers");

        var feathers = tracker.BloodFeathers.ShouldNotBeNull();
        feathers.Max.ShouldBe(BloodFeatherTracker.MaxBloodFeathers);
        feathers.Current.ShouldBe(42);
    }

    private static List<Event> BloodFeatherEvents() =>
    [
        CastWithFeathers(1_000, Spells.HeartSplitter.FSLID, rawAmount: 4_200),
    ];

    private static CastEvent CastWithFeathers(int timestamp, int abilityFslid, int rawAmount)
    {
        var cast = Cast(timestamp, abilityFslid);
        cast.SourceResources = new ActorResources
        {
            HitPoints = 1_000,
            MaxHitPoints = 1_000,
            Resources =
            [
                new ClassResource { Type = ResourceTypes.Tertiary, Amount = rawAmount, Max = 15_000 },
            ],
        };
        return cast;
    }

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        List<Event> events, bool boss, int fightEnd = 10_000)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var fight = new ReportFight(0, "Test", boss ? 1 : 0, true, 0, fightEnd, null, null, null);
        var result = await parser.Analyze(events, Player, fight);
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
        Cast(1_500, Spells.HeartSplitter.FSLID),
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

    private static TEvent Debuff<TEvent>(int timestamp, int abilityFslid, int targetId)
        where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = Player,
        TargetId = targetId,
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
