using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Analysis;

public sealed class RimeAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldIncludeWinterOrbTrackerModule()
    {
        var result = await AnalyzeFixtureAsync();

        var tracker = result.Modules.OfType<WinterOrbTracker>().Single();
        tracker.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_ShouldFindStComboWindows()
    {
        var result = await AnalyzeFixtureAsync();

        var stCombo = result.Modules.OfType<BasicStComboAnalyzer>().Single();
        stCombo.EvaluatedWindows.ShouldBeGreaterThan(0);
        stCombo.PartialWindows.ShouldBeGreaterThan(0);
        stCombo.IgnoredAoeWindows.ShouldBeGreaterThan(0);
        stCombo.ScoreCard.Score.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Analyze_ShouldHaveStatisticsForWinterOrbs()
    {
        var result = await AnalyzeFixtureAsync();

        result.Statistics.ShouldContain(s => s.Module is WinterOrbTracker);
    }

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var result = await AnalyzeFixtureAsync();

        result.GuideComponentType.ShouldNotBeNull();
    }

    private static async Task<HeroAnalysisResult> AnalyzeFixtureAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>("rime");
        var json = File.ReadAllText(GetFixturePath());
        var events = FellowshipLogsFixtureDeserializer.DeserializeEvents(json);
        var fightStartTime = events.Count > 0 ? events[0].Timestamp : 0;
        return await analyzer.Analyze(events, playerId: 3, fightStartTime: fightStartTime);
    }

    private static string GetFixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", "events-with-ability-details.json");
    }
}

