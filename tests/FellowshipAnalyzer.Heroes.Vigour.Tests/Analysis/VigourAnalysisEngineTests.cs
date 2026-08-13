using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Vigour.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class VigourAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldNotProvideGuideComponentType_ForWipHero()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddVigourAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Vigour);
        var result = await analyzer.Analyze([], playerId: 1, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldBeNull();
    }
}
