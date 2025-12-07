using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Vigour.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class VigourAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddVigourAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>("vigour");
        var result = await analyzer.Analyze([], playerId: 1, fightStartTime: 0);

        result.GuideComponentType.ShouldNotBeNull();
    }
}
