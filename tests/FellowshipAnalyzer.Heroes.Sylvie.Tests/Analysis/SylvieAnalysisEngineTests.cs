using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Sylvie.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class SylvieAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ProvidesTheGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddSylvieAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Sylvie);
        var result = await analyzer.Analyze([], playerId: 1, fight: new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public void HeroConfig_DeclaresAMaintainedSeasonThreeExampleReport()
    {
        var config = SylvieCombatLogParser.HeroConfig;

        config.Support.ShouldNotBe(SupportLevel.Unmaintained);
        config.SeasonLabel.ShouldBe(Seasons.Season3);
        config.ExampleReport.ShouldBe("a:gDf7m3N2wvk96dWP/22/118");
        config.Changelog.ShouldNotBeEmpty();
    }
}
