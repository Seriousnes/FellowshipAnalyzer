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

        analyzer.Slaughters[1].OpenWoundsActive.ShouldBeFalse();
        analyzer.Slaughters[1].WellExecuted.ShouldBeFalse();
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

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        List<Event> events, bool boss)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var fight = new ReportFight(0, "Test", boss ? 1 : 0, true, 0, 10_000, null, null, null);
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
        Debuff<RemoveDebuffEvent>(3_000, OpenWoundsFslid, Enemy),
        Cast(5_000, Spells.Slaughter.FSLID),
        Debuff<ApplyDebuffEvent>(5_050, Spells.SlaughterDot.FSLID, Enemy),
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
