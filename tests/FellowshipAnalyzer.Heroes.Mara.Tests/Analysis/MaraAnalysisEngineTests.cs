using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Mara.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldNotProvideGuideComponentType_ForWipHero()
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

        result.GuideComponentType.ShouldBeNull();
    }
}
