using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class MeikoAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMeikoAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>("meiko");
        var result = await analyzer.Analyze([], playerId: 1, fightStartTime: 0);

        result.GuideComponentType.ShouldNotBeNull();
    }
}
