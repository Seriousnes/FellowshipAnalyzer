using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class ArdeosAnalysisEngineTests
{
    private const int PlayerId = 7;
    private const int TargetId = 50;

    private const int SearingBlazeDot = 1_001_820;
    private const int EngulfingFlamesDot = 1_002_325;
    private const int FireBallDot = 1_002_293;
    private const int Detonate = 1_275;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync([], new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_DetonateDotCoverage_MeasuresDotsPerDetonateHit()
    {
        List<Event> events =
        [
            new ApplyDebuffEvent { Timestamp = 100, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = SearingBlazeDot },
            new ApplyDebuffEvent { Timestamp = 110, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = EngulfingFlamesDot },
            new ApplyDebuffEvent { Timestamp = 120, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = FireBallDot },
            new CastEvent { Timestamp = 125, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = Detonate },
            new DamageEvent { Timestamp = 130, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = Detonate, Amount = 100 },
            new DamageEvent { Timestamp = 140, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = Detonate, Amount = 100 },
            new RemoveDebuffEvent { Timestamp = 150, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = FireBallDot },
            new DamageEvent { Timestamp = 160, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = Detonate, Amount = 100 },
        ];

        var (parser, _) = await AnalyzeAsync(events, new ReportFight(0, "", 1, null, 0, 1000, null, null, null));

        var entry = parser.DetonateDotCoverageReports.ShouldHaveSingleItem();
        var report = entry.Result;

        report.DetonateCasts.ShouldBe(1);
        report.DetonateHits.ShouldBe(3);
        report.MaxDotsObserved.ShouldBe(3);
        report.WellLayeredHits.ShouldBe(2);
        report.UnderLayeredHits.ShouldBe(0);
        report.AverageDotsPerHit.ShouldBe(8d / 3d, tolerance: 0.001);
        report.ScoreCard.Score.ShouldBeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_DetonateDotCoverage_ExposesPerPullReadPaths()
    {
        List<Event> events =
        [
            new ApplyDebuffEvent { Timestamp = 100, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = SearingBlazeDot },
            new ApplyDebuffEvent { Timestamp = 110, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = EngulfingFlamesDot },
            new DamageEvent { Timestamp = 130, SourceId = PlayerId, TargetId = TargetId, AbilityGameId = Detonate, Amount = 100 },
        ];

        var (parser, _) = await AnalyzeAsync(events, new ReportFight(0, "", 1, null, 0, 1000, null, null, null));

        var entry = parser.DetonateDotCoverageReports.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);
        entry.Result.DetonateHits.ShouldBe(1);

        pull.DetonateDotCoverageReport.ShouldBe(entry.Result);
        parser.For(pull).DetonateDotCoverageReport.ShouldBe(entry.Result);
    }

    private static async Task<(ArdeosCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
        return (parser, result);
    }
}
