using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Meiko.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class MeikoAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldNotProvideGuideComponentType_ForWipHero()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMeikoAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var analyzer = scope.ServiceProvider.GetRequiredKeyedService<IHeroAnalyzer>(HeroName.Meiko);
        var result = await analyzer.Analyze([], playerId: 1, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldBeNull();
    }
}
