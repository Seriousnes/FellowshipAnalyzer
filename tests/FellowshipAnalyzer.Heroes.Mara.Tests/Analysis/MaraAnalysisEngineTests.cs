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
        var result = await analyzer.Analyze([], playerId: 1, fight: new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ProducesOnePerPull_WithShapeSpecificThresholds()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        parser.MaraResourceDisciplineAnalyzers.Count.ShouldBe(2);

        var single = parser.MaraResourceDisciplineAnalyzers
            .Single(entry => entry.Analyzer.Shape == MaraPullShape.SingleTarget).Analyzer;
        single.ShouldBeOfType<SingleTargetMaraResourceDiscipline>();
        single.FinisherCpThreshold.ShouldBe(5);
        single.Finishers.Count.ShouldBe(2);
        single.FinishersAtThreshold.ShouldBe(1);
        single.EnergyCastsSampled.ShouldBe(3);
        single.EnergyCappedCasts.ShouldBe(1);
        single.GeneratorCasts.ShouldBe(1);
        single.GeneratorOvercapCasts.ShouldBe(1);

        var aoe = parser.MaraResourceDisciplineAnalyzers
            .Single(entry => entry.Analyzer.Shape == MaraPullShape.Aoe).Analyzer;
        aoe.ShouldBeOfType<AoEMaraResourceDiscipline>();
        aoe.FinisherCpThreshold.ShouldBe(4);
        aoe.Finishers.Count.ShouldBe(1);
        aoe.FinishersAtThreshold.ShouldBe(1);
        aoe.MaintenanceFinisherCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.MaraResourceDisciplineAnalyzers
            .Single(e => e.Analyzer.Shape == MaraPullShape.SingleTarget);
        var pull = entry.Pull;

        pull.MaraResourceDisciplineAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MaraResourceDisciplineAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_EvaluatesFinishersAgainstPullThreshold()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var single = parser.MaraResourceDisciplineAnalyzers
            .Single(entry => entry.Analyzer.Shape == MaraPullShape.SingleTarget).Analyzer;
        single.Finishers[0].ComboPoints.ShouldBe(6);
        single.Finishers[0].MeetsThreshold.ShouldBeTrue();
        single.Finishers[1].ComboPoints.ShouldBe(3);
        single.Finishers[1].MeetsThreshold.ShouldBeFalse();

        var aoe = parser.MaraResourceDisciplineAnalyzers
            .Single(entry => entry.Analyzer.Shape == MaraPullShape.Aoe).Analyzer;
        aoe.ShouldNotBeSameAs(single);
        aoe.Finishers.ShouldHaveSingleItem().MeetsThreshold.ShouldBeTrue();
    }

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
    /// Casts whose Energy snapshots chain exactly (each snapshot equals the previous post-cost balance),
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

        var fight = new ReportFight(
            Id: 0, Name: "Fight", EncounterId: 0, Kill: true,
            StartTime: 0, EndTime: 5000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null, InProgress: false,
            DungeonPulls: pulls);

        await parser.Analyze(events, playerId, fight);
        return parser.EnergyComboPointTracker!;
    }

    private static async Task<(MaraCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeFixtureAsync()
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
            new(Id: 1, EncounterId: 42, Kill: true, StartTime: 1000, EndTime: 2000, Name: "Boss", EnemyNpcs: null),
            new(Id: 2, EncounterId: 0, Kill: null, StartTime: 3000, EndTime: 4000, Name: "Trash", EnemyNpcs: null),
        };

        var events = new List<Event>
        {
            Cast(1100, playerId, Spells.QueenFang, comboPoints: 6, energy: 200, maxEnergy: 200),
            Cast(1200, playerId, Spells.QueenFang, comboPoints: 3, energy: 100),
            Cast(1300, playerId, Spells.Backstab, comboPoints: 6, energy: 150),
            Cast(3100, playerId, Spells.ArachnidAssault, comboPoints: 4, energy: 120),
            Cast(3200, playerId, Spells.HemorrhagingStrike, comboPoints: 5, energy: 100),
        };

        var fight = new ReportFight(
            Id: 0, Name: "Fight", EncounterId: 0, Kill: true,
            StartTime: 0, EndTime: 5000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null, InProgress: false,
            DungeonPulls: pulls);

        var result = await parser.Analyze(events, playerId, fight);
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
