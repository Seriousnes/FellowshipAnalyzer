using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class ArdeosAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldNotProvideGuideComponentType_ForWipHero()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Ardeos);
        var result = await analyzer.Analyze([], playerId: 1, fight: new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldBeNull();
    }
}
