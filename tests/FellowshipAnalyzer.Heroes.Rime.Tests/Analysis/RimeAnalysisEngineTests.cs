using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Analysis;

public sealed class RimeAnalysisEngineTests
{
    [Fact]
    public async Task Analyze_ShouldIncludeWinterOrbTrackerModule()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var tracker = result.Modules.OfType<WinterOrbTracker>().Single();
        tracker.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_ShouldFindStComboWindows()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var typed = result.TypedReport.ShouldBeOfType<RimeAnalysisResult>();
        var report = typed.BasicStComboReports.ShouldHaveSingleItem().Result;
        report.EvaluatedWindows.ShouldBeGreaterThan(0);
        report.Windows.ShouldAllBe(window => window.WindowType == BasicStComboAnalyzer.BurstingIceWindowType.SingleTarget);
        report.ScoreCard.Score.ShouldBeInRange(0, 100);
    }

    [Fact]
    public async Task Analyze_WinterOrbAccounting_BalancesGeneratedAgainstSpentWastedAndCurrent()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var tracker = parser.WinterOrbTracker!;
        tracker.Current.ShouldBeInRange(0, tracker.MaxOrbs);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Wasted + tracker.Current);
    }

    [Fact]
    public async Task Analyze_BasicStCombo_ExposesDetectedBuild()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var typed = result.TypedReport.ShouldBeOfType<RimeAnalysisResult>();
        var report = typed.BasicStComboReports.ShouldHaveSingleItem().Result;
        report.Build.ShouldBeOneOf(RimeBuild.Default, RimeBuild.IcyTalons);
        report.Windows.ShouldAllBe(window => window.Build == report.Build);
    }

    [Fact]
    public async Task Analyze_ShouldHaveStatisticsForWinterOrbs()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        result.Statistics.ShouldContain(s => s.Module is WinterOrbTracker);
    }

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_ShouldProduceTypedReportWithPerPullStCombo()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var typed = result.TypedReport.ShouldBeOfType<RimeAnalysisResult>();
        var report = typed.BasicStComboReports.ShouldHaveSingleItem().Result;
        report.EvaluatedWindows.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_BasicStCombo_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.BasicStComboReports.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);
        entry.Result.EvaluatedWindows.ShouldBeGreaterThan(0);

        // The three read paths agree: cross-pull index, pull.X extension, parser.For(pull).X.
        pull.BasicStComboReport.ShouldBe(entry.Result);
        parser.For(pull).BasicStComboReport.ShouldBe(entry.Result);
    }

    private static async Task<(RimeCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeFixtureAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        var json = File.ReadAllText(GetFixturePath());
        using var doc = JsonDocument.Parse(json);
        var eventsEl = doc.RootElement
            .GetProperty("data")
            .GetProperty("reportData")
            .GetProperty("report")
            .GetProperty("events")
            .GetProperty("data");

        var jsonOptions = new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        var jsonContext = new FellowshipAnalyzerJsonContext(jsonOptions);
        var events = JsonSerializer.Deserialize(eventsEl, jsonContext.ListEvent)!;
        var fightStartTime = events.Count > 0 ? events.Min(e => e.Timestamp) : 0;
        var fightEndTime = events.Count > 0 ? events.Max(e => e.Timestamp) : 0;
        var fight = new ReportFight(0, "", 1, null, fightStartTime, fightEndTime, null, null, null);
        var result = await parser.Analyze(events, playerId: 3, fight: fight);
        return (parser, result);
    }

    private static string GetFixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", "events-with-ability-details.json");
    }
}
