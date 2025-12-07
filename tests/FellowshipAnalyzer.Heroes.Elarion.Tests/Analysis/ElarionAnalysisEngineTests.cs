using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Analysis;

public sealed class ElarionAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>("elarion");
        var result = await analyzer.Analyze([], playerId: 1, fightStartTime: 0);

        result.GuideComponentType.ShouldNotBeNull();
    }
}
