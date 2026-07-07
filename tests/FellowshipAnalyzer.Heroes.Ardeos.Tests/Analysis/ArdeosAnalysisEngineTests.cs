using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class ArdeosAnalysisEngineTests
{
    private const int PlayerId = 7;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync([], new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_CleanWildfireWindow_IsScoredSuccessful()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            Cast(Spells.FireFrogs.FSLID, anchor - 5000),
            Cast(Spells.Apocalypse.FSLID, anchor - 4000),
            Cast(Spells.FireBall.FSLID, anchor - 3000),
            Cast(Spells.EngulfingFlames.FSLID, anchor - 2000),
            Cast(Spells.EngulfingFlames.FSLID, anchor - 1500),
            Cast(Spells.Pyromania.FSLID, anchor - 1000),
            Cast(Spells.Wildfire.FSLID, anchor),
            Cast(Spells.Detonate.FSLID, anchor + 500),
            Cast(Spells.Detonate.FSLID, anchor + 1000),
            Cast(Spells.Detonate.FSLID, anchor + 1500),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var entry = parser.WildfireComboReports.ShouldHaveSingleItem();
        var report = entry.Result;
        report.EvaluatedWindows.ShouldBe(1);
        report.SuccessfulWindows.ShouldBe(1);
        report.PartialWindows.ShouldBe(0);
        report.WindowsWithPyromania.ShouldBe(1);
        report.ScoreCard.Score.ShouldBeInRange(0, 100);
        report.ScoreCard.Score.ShouldBe(100);

        var window = report.Windows.ShouldHaveSingleItem();
        window.CoreSetupCount.ShouldBe(4);
        window.DetonateCasts.ShouldBe(3);
        window.HasPyromania.ShouldBeTrue();
        window.Successful.ShouldBeTrue();

        var pull = entry.Pull;
        pull.WildfireComboReport.ShouldBe(report);
        parser.For(pull).WildfireComboReport.ShouldBe(report);
    }

    [Fact]
    public async Task Analyze_WindowWithoutDetonateFollowUp_IsNotSuccessful()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            Cast(Spells.FireFrogs.FSLID, anchor - 5000),
            Cast(Spells.Apocalypse.FSLID, anchor - 4000),
            Cast(Spells.FireBall.FSLID, anchor - 3000),
            Cast(Spells.EngulfingFlames.FSLID, anchor - 2000),
            Cast(Spells.Wildfire.FSLID, anchor),
            Cast(Spells.Detonate.FSLID, anchor + 500),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var report = parser.WildfireComboReports.ShouldHaveSingleItem().Result;
        report.EvaluatedWindows.ShouldBe(1);
        report.SuccessfulWindows.ShouldBe(0);
        report.PartialWindows.ShouldBe(0);
        report.ScoreCard.Score.ShouldBe(0);
        report.ScoreCard.Accent.ShouldBe("ember");

        var window = report.Windows.ShouldHaveSingleItem();
        window.CoreSetupCount.ShouldBe(4);
        window.DetonateCasts.ShouldBe(1);
        window.Successful.ShouldBeFalse();
        window.Partial.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_WindowMissingSetupButDetonateSpam_IsPartial()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            Cast(Spells.FireFrogs.FSLID, anchor - 5000),
            Cast(Spells.Wildfire.FSLID, anchor),
            Cast(Spells.Detonate.FSLID, anchor + 500),
            Cast(Spells.Detonate.FSLID, anchor + 1000),
            Cast(Spells.Detonate.FSLID, anchor + 1500),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var report = parser.WildfireComboReports.ShouldHaveSingleItem().Result;
        report.EvaluatedWindows.ShouldBe(1);
        report.SuccessfulWindows.ShouldBe(0);
        report.PartialWindows.ShouldBe(1);
        report.ScoreCard.Score.ShouldBe(50);
        report.ScoreCard.Accent.ShouldBe("amber");

        var window = report.Windows.ShouldHaveSingleItem();
        window.CoreSetupCount.ShouldBe(1);
        window.DetonateCasts.ShouldBe(3);
        window.Partial.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_MultipleWildfireAnchors_AreEvaluatedIndependently()
    {
        var events = new List<Event>();
        AppendCleanWindow(events, 10000);
        events.Add(Cast(Spells.FireFrogs.FSLID, 60000 - 5000));
        events.Add(Cast(Spells.Wildfire.FSLID, 60000));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 80000));

        var report = parser.WildfireComboReports.ShouldHaveSingleItem().Result;
        report.EvaluatedWindows.ShouldBe(2);
        report.SuccessfulWindows.ShouldBe(1);
        report.PartialWindows.ShouldBe(0);
        report.ScoreCard.Score.ShouldBe(50);
        report.ScoreCard.Accent.ShouldBe("amber");
        report.Windows.Count.ShouldBe(2);
    }

    private static void AppendCleanWindow(List<Event> events, int anchor)
    {
        events.Add(Cast(Spells.FireFrogs.FSLID, anchor - 5000));
        events.Add(Cast(Spells.Apocalypse.FSLID, anchor - 4000));
        events.Add(Cast(Spells.FireBall.FSLID, anchor - 3000));
        events.Add(Cast(Spells.EngulfingFlames.FSLID, anchor - 2000));
        events.Add(Cast(Spells.Wildfire.FSLID, anchor));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 500));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1000));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1500));
    }

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static ReportFight SpanningFight(double startTime, double endTime) =>
        new(0, "", 0, null, startTime, endTime, null, null, null);

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
