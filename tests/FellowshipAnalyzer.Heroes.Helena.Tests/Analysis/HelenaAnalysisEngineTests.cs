using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Helena.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class HelenaAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ProvidesTheGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddHelenaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Helena);
        var result = await analyzer.Analyze([], playerId: 1, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public void HeroConfig_DeclaresAMaintainedSeasonThreeExampleReport()
    {
        var config = HelenaCombatLogParser.HeroConfig;

        config.Support.ShouldNotBe(SupportLevel.None);
        config.Maintainers.ShouldNotBeEmpty();
        config.SeasonLabel.ShouldBe(Seasons.Season3);
        config.ExampleReport.ShouldBe("a:gDf7m3N2wvk96dWP/22/109");
        config.Changelog.ShouldNotBeEmpty();
    }
}
