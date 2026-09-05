using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraAnalysisEngineTests
{
    private const int BossPullId = 1;
    private const int TrashPullId = 2;
    private const int BossNpcId = 900;
    private const int TrashNpcId = 901;
    private const int BossPullEnemies = 1;
    private const int TrashPullEnemies = 4;

    private static DungeonPullNpc Npc(int id, int instances) =>
        new(id, GameId: id, MinimumInstanceId: 1, MaximumInstanceId: instances, null, null);

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Mara);
        var result = await analyzer.Analyze([], playerId: 1, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ProducesOnePerPull()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        parser.MaraResourceDisciplineAnalyzers.Count.ShouldBe(2);

        var boss = AnalyzerForPull(parser, BossPullId);
        boss.Finishers.Count.ShouldBe(3);
        boss.FinishersAtThreshold.ShouldBe(2);
        boss.EnergyCastsSampled.ShouldBe(4);
        boss.EnergyCappedCasts.ShouldBe(1);
        boss.GeneratorCasts.ShouldBe(1);
        boss.GeneratorOvercapCasts.ShouldBe(1);

        var trash = AnalyzerForPull(parser, TrashPullId);
        trash.Finishers.Count.ShouldBe(2);
        trash.FinishersAtThreshold.ShouldBe(1);
        trash.MaintenanceFinisherCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.MaraResourceDisciplineAnalyzers.Single(e => e.Pull.Id == BossPullId);
        var pull = entry.Pull;

        pull.MaraResourceDisciplineAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MaraResourceDisciplineAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_HoldsQueenFangToFiveOnEveryPullShape()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var boss = AnalyzerForPull(parser, BossPullId);
        boss.QueenFangCasts.ShouldBe(2);
        boss.QueenFangAtThreshold.ShouldBe(1);
        boss.Finishers[0].ComboPoints.ShouldBe(6);
        boss.Finishers[0].MeetsThreshold.ShouldBeTrue();
        boss.Finishers[1].ComboPoints.ShouldBe(3);
        boss.Finishers[1].MeetsThreshold.ShouldBeFalse();

        var trash = AnalyzerForPull(parser, TrashPullId);
        var queenFang = trash.Finishers.Single(finisher => finisher.AbilityId == Spells.QueenFang.Id);
        queenFang.ComboPoints.ShouldBe(4);
        queenFang.Threshold.ShouldBe(MaraResourceDisciplineAnalyzer.QueenFangThreshold);
        queenFang.MeetsThreshold.ShouldBeFalse();
        trash.QueenFangAtThreshold.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_HoldsArachnidAssaultToFourOnEveryPullShape()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var boss = AnalyzerForPull(parser, BossPullId);
        var onBoss = boss.Finishers.Single(finisher => finisher.AbilityId == Spells.ArachnidAssault.Id);
        onBoss.ComboPoints.ShouldBe(4);
        onBoss.Threshold.ShouldBe(MaraResourceDisciplineAnalyzer.ArachnidAssaultThreshold);
        onBoss.MeetsThreshold.ShouldBeTrue();
        boss.ArachnidAssaultAtThreshold.ShouldBe(1);

        var trash = AnalyzerForPull(parser, TrashPullId);
        trash.ArachnidAssaultCasts.ShouldBe(1);
        trash.ArachnidAssaultAtThreshold.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ReadsEnemiesAliveAtEachFinisher()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        AnalyzerForPull(parser, BossPullId).Finishers
            .ShouldAllBe(finisher => finisher.EnemiesAlive == BossPullEnemies);
        AnalyzerForPull(parser, TrashPullId).Finishers
            .ShouldAllBe(finisher => finisher.EnemiesAlive == TrashPullEnemies);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_FlagsTheFinisherTheEnemyCountDoesNotCallFor()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var boss = AnalyzerForPull(parser, BossPullId);
        boss.ArachnidAssaultTargetThreshold.ShouldBe(MaraResourceDisciplineAnalyzer.ArachnidAssaultTargets);
        boss.FinishersWithTargetCount.ShouldBe(3);
        boss.FinishersMatchingTargetCount.ShouldBe(2);
        boss.ArachnidAssaultBelowTargetThreshold.ShouldBe(1);
        boss.QueenFangAboveTargetThreshold.ShouldBe(0);

        var trash = AnalyzerForPull(parser, TrashPullId);
        trash.FinishersWithTargetCount.ShouldBe(2);
        trash.FinishersMatchingTargetCount.ShouldBe(1);
        trash.QueenFangAboveTargetThreshold.ShouldBe(1);
        trash.ArachnidAssaultBelowTargetThreshold.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_FeedTheQueenRaisesTheArachnidAssaultTargetThreshold()
    {
        var (parser, _) = await AnalyzeFixtureAsync(feedTheQueen: true);

        var trash = AnalyzerForPull(parser, TrashPullId);
        trash.ArachnidAssaultTargetThreshold
            .ShouldBe(MaraResourceDisciplineAnalyzer.ArachnidAssaultTargetsWithFeedTheQueen);

        trash.FinishersMatchingTargetCount.ShouldBe(1);
        trash.QueenFangAboveTargetThreshold.ShouldBe(0);
        trash.ArachnidAssaultBelowTargetThreshold.ShouldBe(1);
    }

    private static MaraResourceDisciplineAnalyzer AnalyzerForPull(MaraCombatLogParser parser, int pullId) =>
        parser.MaraResourceDisciplineAnalyzers.Single(entry => entry.Pull.Id == pullId).Analyzer;

    [Fact]
    public async Task Analyze_ShouldIncludeEnergyComboPointTrackerModule()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var tracker = result.Modules.OfType<EnergyComboPointTracker>().Single();
        tracker.GetDisplayName(ResourceTypes.Primary).ShouldBe("Energy");
        tracker.GetDisplayName(ResourceTypes.Secondary).ShouldBe("Combo Points");
    }

    [Fact]
    public async Task Analyze_ShouldCollectEnergyComboPointStatistics()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var entry = result.Statistics.Single(statistic => statistic.Module is EnergyComboPointTracker);
        entry.Category.ShouldBe(StatisticCategory.Resources);
    }

    [Fact]
    public async Task Analyze_EnergyAccounting_BalancesGeneratedAgainstSpentAndCurrent()
    {
        var tracker = await AnalyzeTrackerFixtureAsync();

        tracker.EnergyGenerated.ShouldBe(100);
        tracker.EnergySpent.ShouldBe(80);
        tracker.EnergyWasted.ShouldBe(0);
        tracker.EnergyCurrent.ShouldBe(20);
        tracker.EnergyGenerated.ShouldBe(tracker.EnergySpent + tracker.EnergyCurrent);
    }

    [Fact]
    public async Task Analyze_EnergySpend_ResolvesCostsFromTheSpellRegistry()
    {
        var tracker = await AnalyzeTrackerFixtureAsync();

        var spenders = tracker.GetSpenderCasts(ResourceTypes.Primary);
        spenders[Spells.Backstab.Id].ShouldBe(2);
        spenders[Spells.QueenFang.Id].ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ComboPoints_AreCappedAtSixAndNeverSpend()
    {
        var tracker = await AnalyzeTrackerFixtureAsync();

        tracker.ComboPoints.ShouldNotBeNull();
        tracker.ComboPoints!.Max.ShouldBe(tracker.MaxComboPointCount);
        tracker.ComboPointsGenerated.ShouldBe(6);
        tracker.ComboPointsCurrent.ShouldBe(6);
        tracker.GetSpent(ResourceTypes.Secondary).ShouldBe(0);
    }

    /// <summary>
    /// Casts whose Energy amounts chain exactly (each equals the previous post-cost balance),
    /// so every decrease is accounted for by a registry cost and the tracker's accounting identity holds.
    /// </summary>
    private static async Task<EnergyComboPointTracker> AnalyzeTrackerFixtureAsync()
    {
        const int playerId = 7;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();

        var events = new List<Event>
        {
            Cast(1100, playerId, Spells.Backstab, comboPoints: 2, energy: 100),
            Cast(1200, playerId, Spells.Backstab, comboPoints: 4, energy: 80),
            Cast(1300, playerId, Spells.QueenFang, comboPoints: 6, energy: 60),
        };

        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 42, Kill: true, StartTime: 1000, EndTime: 2000, Name: "Boss", EnemyNpcs: null),
        };

        var dungeon = new ReportDungeon(
            Id: 0, Name: "Dungeon", EncounterId: 0, Kill: true,
            StartTime: 0, EndTime: 5000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
            DungeonPulls: pulls);

        await parser.Analyze(events, playerId, dungeon);
        return parser.EnergyComboPointTracker!;
    }

    private static async Task<(MaraCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeFixtureAsync(
        bool feedTheQueen = false)
    {
        const int playerId = 7;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();

        var pulls = new List<DungeonPull>
        {
            new(Id: BossPullId, EncounterId: 42, Kill: true, StartTime: 1000, EndTime: 2000, Name: "Boss",
                EnemyNpcs: [Npc(BossNpcId, instances: BossPullEnemies)]),
            new(Id: TrashPullId, EncounterId: 0, Kill: null, StartTime: 3000, EndTime: 4000, Name: "Trash",
                EnemyNpcs: [Npc(TrashNpcId, instances: TrashPullEnemies)]),
        };

        var events = new List<Event>
        {
            new CombatantInfoEvent
            {
                SourceId = playerId,
                Talents = feedTheQueen ? [new TalentInfo { Id = MaraTalents.FeedTheQueen }] : [],
            },
            Cast(1100, playerId, Spells.QueenFang, comboPoints: 6, energy: 200, maxEnergy: 200),
            Cast(1200, playerId, Spells.QueenFang, comboPoints: 3, energy: 100),
            Cast(1300, playerId, Spells.Backstab, comboPoints: 6, energy: 150),
            Cast(1400, playerId, Spells.ArachnidAssault, comboPoints: 4, energy: 130),
            Cast(3100, playerId, Spells.ArachnidAssault, comboPoints: 4, energy: 120),
            Cast(3200, playerId, Spells.HemorrhagingStrike, comboPoints: 5, energy: 100),
            Cast(3300, playerId, Spells.QueenFang, comboPoints: 4, energy: 90),
        };

        var dungeon = new ReportDungeon(
            Id: 0, Name: "Dungeon", EncounterId: 0, Kill: true,
            StartTime: 0, EndTime: 5000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
            DungeonPulls: pulls);

        var result = await parser.Analyze(events, playerId, dungeon);
        return (parser, result);
    }

    /// <summary>
    /// Raw Fellowship log resource values are scaled x100; the ResourceNormalizer divides by 100
    /// during Analyze, so the fixture stores in-game intent (0-6 combo points, 0-200 Energy) x100.
    /// </summary>
    private static CastEvent Cast(int timestamp, int playerId, Spell spell, int? comboPoints, int energy, int maxEnergy = 200)
    {
        var resources = new List<ClassResource>
        {
            new() { Type = ResourceTypes.Primary, Amount = energy * 100, Max = maxEnergy * 100 },
        };
        if (comboPoints is not null)
            resources.Add(new() { Type = ResourceTypes.Secondary, Amount = comboPoints.Value * 100, Max = 600 });

        return new CastEvent
        {
            Timestamp = timestamp,
            SourceId = playerId,
            Ability = new Ability { Id = spell.Id },
            SourceResources = new ActorResources { Resources = resources },
        };
    }
}
