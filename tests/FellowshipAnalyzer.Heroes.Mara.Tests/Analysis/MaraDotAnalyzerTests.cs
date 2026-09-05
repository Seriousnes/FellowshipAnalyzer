using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraDotAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BossId = 99;
    private const int AddId = 50;
    private const int DungeonEnd = 20000;

    [Fact]
    public async Task Analyze_SeethingPoisonReapplied_MeasuresUptimeAndGap()
    {
        var events = new List<Event>
        {
            Apply(Spells.WidowBitePoison, BossId, 1000),
            Remove(Spells.WidowBitePoison, BossId, 5000),
            Apply(Spells.WidowBitePoison, BossId, 8000),
            Tick(Spells.WidowBitePoison, BossId, DungeonEnd),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var entry = parser.MaraDotAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<MaraDotUptimeAnalyzer>();

        var seething = analyzer.SeethingPoison;
        seething.Dot.ShouldBe(MaraDots.SeethingPoison);
        seething.Windows.Count.ShouldBe(2);
        seething.Uptime.ShouldBe(0.8, 0.0001);
        seething.GapCount.ShouldBe(1);
        seething.TotalGapMs.ShouldBe(3000);

        analyzer.Hemorrhage.Windows.ShouldBeEmpty();
        analyzer.Hemorrhage.Uptime.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task Analyze_BossPull_ExposesPerPullReadPaths()
    {
        var events = new List<Event> { Apply(Spells.WidowBitePoison, BossId, 1000) };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var entry = parser.MaraDotAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.MaraDotAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MaraDotAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_OpenWindow_ClosesAtTheLastTick()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000),
            Tick(Spells.HemorrhagingStrikeBleed, BossId, 10000),
            Tick(Spells.HemorrhagingStrikeBleed, BossId, 19000),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Hemorrhage.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1000);
        window.End.ShouldBe(19000);
        analyzer.Hemorrhage.Uptime.ShouldBe(0.9, 0.0001);
        analyzer.Hemorrhage.GapCount.ShouldBe(0);
        analyzer.Hemorrhage.TotalGapMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_OpenWindowThatStopsTicking_DoesNotRunToPullEnd()
    {
        var events = new List<Event> { Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000) };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Hemorrhage.Windows.ShouldBeEmpty();
        analyzer.Hemorrhage.Uptime.ShouldBe(0d, 0.0001);
        analyzer.Hemorrhage.PrimaryTarget.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_AddsWithTheBleed_DoNotDiluteBossUptime()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000),
            Apply(Spells.HemorrhagingStrikeBleed, AddId, 3000),
            Remove(Spells.HemorrhagingStrikeBleed, AddId, 5000),
            Remove(Spells.HemorrhagingStrikeBleed, BossId, 11000),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        var window = analyzer.Hemorrhage.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1000);
        window.End.ShouldBe(11000);
        analyzer.Hemorrhage.Uptime.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task Analyze_HemorrhageDuration_ComesFromTheComboPointSnapshot()
    {
        var events = new List<Event>
        {
            Strike(900, comboPoints: 4),
            Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000),
            Strike(4900, comboPoints: 2),
            Refresh(Spells.HemorrhagingStrikeBleed, BossId, 5000),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.HemorrhageApplications.Count.ShouldBe(2);

        analyzer.HemorrhageApplications[0].Timestamp.ShouldBe(1000);
        analyzer.HemorrhageApplications[0].ComboPoints.ShouldBe(4);
        analyzer.HemorrhageApplications[0].ExpectedDurationMs.ShouldBe(24000);
        analyzer.HemorrhageApplications[0].Refresh.ShouldBeFalse();

        analyzer.HemorrhageApplications[1].ComboPoints.ShouldBe(2);
        analyzer.HemorrhageApplications[1].ExpectedDurationMs.ShouldBe(18000);
        analyzer.HemorrhageApplications[1].Refresh.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_HemorrhageRefresh_RecordsTimeLeftOnTheOverwrittenBleed()
    {
        var events = new List<Event>
        {
            Strike(900, comboPoints: 4),
            Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000),
            Strike(4900, comboPoints: 2),
            Refresh(Spells.HemorrhagingStrikeBleed, BossId, 5000),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var refresh = SingleUptimeAnalyzer(parser).HemorrhageRefreshes.ShouldHaveSingleItem();
        refresh.Timestamp.ShouldBe(5000);
        refresh.RemainingMs.ShouldBe(20000);
    }

    [Fact]
    public async Task Analyze_HemorrhageRefreshPastExpiry_ReportsNoTimeLeft()
    {
        var events = new List<Event>
        {
            Strike(900, comboPoints: 0),
            Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000),
            Strike(14900, comboPoints: 0),
            Refresh(Spells.HemorrhagingStrikeBleed, BossId, 15000),
        };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.HemorrhageApplications[0].ExpectedDurationMs.ShouldBe(MaraDotUptimeAnalyzer.HemorrhageBaseDurationMs);
        analyzer.HemorrhageRefreshes.ShouldHaveSingleItem().RemainingMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_HemorrhageWithoutAStrikeSnapshot_UsesTheBaseDuration()
    {
        var events = new List<Event> { Apply(Spells.HemorrhagingStrikeBleed, BossId, 1000) };

        var parser = await AnalyzeAsync(events, BossDungeon());

        var application = SingleUptimeAnalyzer(parser).HemorrhageApplications.ShouldHaveSingleItem();
        application.ComboPoints.ShouldBe(0);
        application.ExpectedDurationMs.ShouldBe(MaraDotUptimeAnalyzer.HemorrhageBaseDurationMs);
    }

    [Fact]
    public async Task Analyze_NoApplications_ReportsZeroForEveryMaintainedEffect()
    {
        var parser = await AnalyzeAsync([], BossDungeon());

        var analyzer = SingleUptimeAnalyzer(parser);
        analyzer.Uptimes.Count.ShouldBe(MaraDots.Maintained.Count);
        foreach (var entry in analyzer.Uptimes)
        {
            entry.Windows.ShouldBeEmpty();
            entry.Uptime.ShouldBe(0d, 0.0001);
            entry.GapCount.ShouldBe(0);
            entry.TotalGapMs.ShouldBe(0);
        }
    }

    [Fact]
    public async Task Analyze_TrashPull_IsNotScoredForUptime()
    {
        var events = new List<Event> { Apply(Spells.WidowBitePoison, BossId, 1000) };

        var parser = await AnalyzeAsync(events, TrashDungeon(4));

        parser.MaraDotAnalyzers.Select(entry => entry.Analyzer).OfType<MaraDotUptimeAnalyzer>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_SpreadAcrossPack_MeasuresBleedCoverage()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, 10, 1000),
            Apply(Spells.HemorrhagingStrikeBleed, 11, 1500),
            Apply(Spells.HemorrhagingStrikeBleed, 12, 2000),
            Refresh(Spells.HemorrhagingStrikeBleed, 10, 2500),
            Apply(Spells.WidowBitePoison, 10, 3000),
        };

        var parser = await AnalyzeAsync(events, TrashDungeon(5));

        var entry = parser.MaraDotAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<MaraDotSpreadAnalyzer>();
        analyzer.HemorrhageTargets.ShouldBe(3);
        analyzer.HemorrhageApplications.ShouldBe(3);
        analyzer.SeethingPoisonTargets.ShouldBe(1);
        analyzer.TargetCount.ShouldBe(5);
        analyzer.RosterSize.ShouldBe(5);
        analyzer.Coverage.ShouldBe(0.6, 0.0001);

        var pull = entry.Pull;
        pull.MaraDotAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).MaraDotAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task Analyze_VolatilePoisonReapplied_IsCounted()
    {
        var events = new List<Event>
        {
            Apply(Spells.SkitteringBladesPoison, 10, 1000),
            Apply(Spells.SkitteringBladesPoison, 11, 1200),
            Refresh(Spells.SkitteringBladesPoison, 10, 3000),
            Refresh(Spells.SkitteringBladesPoison, 10, 5000),
        };

        var parser = await AnalyzeAsync(events, TrashDungeon(4));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.VolatilePoisonTargets.ShouldBe(2);
        analyzer.VolatilePoisonApplications.ShouldBe(2);
        analyzer.VolatilePoisonRefreshes.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_SameIdDistinctInstance_CountsSeparately()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, 10, 1000, targetInstance: 0),
            Apply(Spells.HemorrhagingStrikeBleed, 10, 1500, targetInstance: 1),
        };

        var parser = await AnalyzeAsync(events, TrashDungeon(4));

        SingleSpreadAnalyzer(parser).HemorrhageTargets.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_UnreportedRoster_FallsBackToTheEnemiesEngaged()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, 10, 1000),
            Apply(Spells.HemorrhagingStrikeBleed, 11, 1500),
            Apply(Spells.WidowBitePoison, 12, 2000),
            Apply(Spells.SkitteringBladesPoison, 13, 2500),
        };

        var parser = await AnalyzeAsync(events, SpanningDungeon());

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.TargetCount.ShouldBe(0);
        analyzer.EnemiesEngaged.ShouldBe(4);
        analyzer.RosterSize.ShouldBe(4);
        analyzer.HemorrhageTargets.ShouldBe(2);
        analyzer.Coverage.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task Analyze_EnemySourcedAndForeignAbilities_AreNotCounted()
    {
        var events = new List<Event>
        {
            Apply(Spells.HemorrhagingStrikeBleed, 10, 1000),
            EnemySourced(Spells.HemorrhagingStrikeBleed, 20, 1500),
            Apply(Spells.HemorrhagingStrikeBuff, 30, 2000),
        };

        var parser = await AnalyzeAsync(events, TrashDungeon(5));

        var analyzer = SingleSpreadAnalyzer(parser);
        analyzer.HemorrhageTargets.ShouldBe(1);
        analyzer.HemorrhageApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_BossPull_IsNotScoredForSpread()
    {
        var events = new List<Event> { Apply(Spells.HemorrhagingStrikeBleed, 10, 1000) };

        var parser = await AnalyzeAsync(events, BossDungeon());

        parser.MaraDotAnalyzers.Select(entry => entry.Analyzer).OfType<MaraDotSpreadAnalyzer>().ShouldBeEmpty();
    }

    private static MaraDotUptimeAnalyzer SingleUptimeAnalyzer(MaraCombatLogParser parser) =>
        parser.MaraDotAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<MaraDotUptimeAnalyzer>();

    private static MaraDotSpreadAnalyzer SingleSpreadAnalyzer(MaraCombatLogParser parser) =>
        parser.MaraDotAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<MaraDotSpreadAnalyzer>();

    private static ApplyDebuffEvent Apply(Spell effect, int targetId, int timestamp, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static RefreshDebuffEvent Refresh(Spell effect, int targetId, int timestamp, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static RemoveDebuffEvent Remove(Spell effect, int targetId, int timestamp, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static DamageEvent Tick(Spell effect, int targetId, int timestamp, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static ApplyDebuffEvent EnemySourced(Spell effect, int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = BossId,
        TargetId = targetId,
        Ability = new Ability { Id = effect.FSLID },
    };

    /// <summary>
    /// A Hemorrhaging Strike cast with the combo points it spends. Raw Fellowship log resource
    /// values are scaled x100 and the ResourceNormalizer divides by 100 during Analyze, so the fixture
    /// stores in-game intent (0-6 combo points, 0-200 Energy) x100.
    /// </summary>
    private static CastEvent Strike(int timestamp, int comboPoints) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = Spells.HemorrhagingStrike.Id },
        SourceResources = new ActorResources
        {
            Resources =
            [
                new() { Type = ResourceTypes.Primary, Amount = 10000, Max = 20000 },
                new() { Type = ResourceTypes.Secondary, Amount = comboPoints * 100, Max = 600 },
            ],
        },
    };

    private static ReportDungeon BossDungeon() =>
        new(0, "", 1, null, 0, DungeonEnd, null, null, null);

    private static ReportDungeon TrashDungeon(int enemies) =>
        new(0, "", 0, null, 0, DungeonEnd, null, null, null, EnemyNpcs: [new DungeonNpc(1, 100, enemies, 1, null)]);

    private static ReportDungeon SpanningDungeon() =>
        new(0, "", 0, null, 0, DungeonEnd, null, null, null);

    private static async Task<MaraCombatLogParser> AnalyzeAsync(List<Event> events, ReportDungeon dungeon)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze(events, PlayerId, dungeon);
        return parser;
    }
}
