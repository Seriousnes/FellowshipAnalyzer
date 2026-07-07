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

        parser.MaraResourceReports.Count.ShouldBe(2);

        var single = parser.MaraResourceReports.Single(r => r.Result.Shape == MaraPullShape.SingleTarget).Result;
        single.FinisherCpThreshold.ShouldBe(5);
        single.FinishersWithData.ShouldBe(2);
        single.FinishersAtThreshold.ShouldBe(1);
        single.EnergyCastsSampled.ShouldBe(3);
        single.EnergyCappedCasts.ShouldBe(1);
        single.GeneratorCasts.ShouldBe(1);
        single.GeneratorOvercapCasts.ShouldBe(1);

        var aoe = parser.MaraResourceReports.Single(r => r.Result.Shape == MaraPullShape.Aoe).Result;
        aoe.FinisherCpThreshold.ShouldBe(4);
        aoe.FinishersWithData.ShouldBe(1);
        aoe.FinishersAtThreshold.ShouldBe(1);
        aoe.MaintenanceFinisherCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.MaraResourceReports.Single(r => r.Result.Shape == MaraPullShape.SingleTarget);
        var pull = entry.Pull;

        pull.MaraResourceReport.ShouldBe(entry.Result);
        parser.For(pull).MaraResourceReport.ShouldBe(entry.Result);
    }

    [Fact]
    public async Task Analyze_ResourceDiscipline_TypedReportCarriesResults()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var typed = result.TypedReport.ShouldBeOfType<MaraAnalysisResult>();
        typed.MaraResourceReports.Count.ShouldBe(2);
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
            // Single-target (boss) pull: one on-threshold finisher at capped Energy, one under threshold, one overcap generator.
            Cast(1100, playerId, Spells.QueenFang, comboPoints: 6, energy: 200, maxEnergy: 200),
            Cast(1200, playerId, Spells.QueenFang, comboPoints: 3, energy: 100),
            Cast(1300, playerId, Spells.Backstab, comboPoints: 6, energy: 150),

            // Multi-target (trash) pull: one on-threshold AoE finisher plus a maintenance Hemorrhaging Strike.
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

    // Raw Fellowship log resource values are scaled ×100; the ResourceNormalizer divides by 100
    // during Analyze, so the fixture stores in-game intent (0–6 combo points, 0–200 Energy) ×100.
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
