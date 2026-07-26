using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

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
    public async Task Analyze_ShouldFindWintersEmbraceWindows()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.EvaluatedWindows.ShouldBe(87);
        analyzer.Windows.Count.ShouldBe(analyzer.EvaluatedWindows);
        analyzer.Windows.ShouldAllBe(
            window => window is SingleTargetEmbraceWindowAnalyzer.SingleTargetWindowEvaluation);
        analyzer.Windows.ShouldAllBe(window => !window.BoundaryTruncated);
    }

    [Fact]
    public async Task Analyze_WinterOrbAccounting_BalancesGeneratedAgainstSpentWastedAndCurrent()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var tracker = parser.WinterOrbTracker!;
        tracker.Current.ShouldBeInRange(0, tracker.MaxOrbs);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Wasted + tracker.Current);
        tracker.OvercapIncidents.Count.ShouldBe(tracker.Wasted);
    }

    [Fact]
    public async Task Analyze_WinterOrbTracker_FixtureCarriesNoOrbSnapshotsSoTheWholePoolReadsZero()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var tracker = parser.WinterOrbTracker!;
        tracker.Generated.ShouldBe(0);
        tracker.Spent.ShouldBe(0);
        tracker.Wasted.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.CappedMs.ShouldBe(0);
        tracker.OvercapIncidents.ShouldBeEmpty();
        tracker.MaxOrbs.ShouldBe(5);
        tracker.GetDisplayName(ResourceTypes.Tertiary).ShouldBe("Winter Orbs");
    }

    [Fact]
    public async Task Analyze_WintersEmbrace_DetectsDefaultBuildForFixturePlayer()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Build.ShouldBe(RimeBuild.Default);
        analyzer.StSpenderName.ShouldBe("Glacial Blast");
        analyzer.AoeSpenderName.ShouldBe("Ice Comet");
        analyzer.Windows.ShouldAllBe(window => window.Build == analyzer.Build);
    }

    [Fact]
    public async Task Analyze_WintersEmbrace_FixtureCarriesNoOrbSnapshotsSoNoWindowReadsAsStarved()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsWithOrbData.ShouldBe(0);
        analyzer.Windows.ShouldAllBe(window => window.OrbsBankedAtOpen == null);
        analyzer.StarvedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WintersEmbrace_FixtureBurstingIceTargetNeverDiesMidWindow()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsEndedByTargetDeath.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_MajorCooldowns_FindsWindowsForEveryMajor()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.MajorCooldownAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.WindowsFor(RimeMajorCooldown.IceBlitz).Count.ShouldBe(12);
        analyzer.WindowsFor(RimeMajorCooldown.WintersBlessing).Count.ShouldBe(22);
        analyzer.WindowsFor(RimeMajorCooldown.FlightOfTheNavir).Count.ShouldBe(22);
        analyzer.WindowsFor(RimeMajorCooldown.WrathOfWinter).Count.ShouldBe(21);
        analyzer.Windows.Count.ShouldBe(77);
        analyzer.Windows.ShouldAllBe(window => !window.BoundaryTruncated);
    }

    [Fact]
    public async Task Analyze_MajorCooldowns_TracksCastsAndBuffUptimeForEveryCooldownGatedMajor()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.MajorCooldownAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.PullDurationMs.ShouldBeGreaterThan(0);

        foreach (var major in new[]
        {
            RimeMajorCooldown.IceBlitz,
            RimeMajorCooldown.WintersBlessing,
            RimeMajorCooldown.FlightOfTheNavir,
        })
        {
            var usage = analyzer.UsageFor(major);
            usage.Casts.ShouldBeGreaterThan(0);
            usage.HeldMs.ShouldBeGreaterThanOrEqualTo(0);
            usage.BuffUptimeMs.ShouldBeGreaterThan(0);
            usage.BuffUptimeMs.ShouldBeLessThanOrEqualTo(analyzer.PullDurationMs);
        }
    }

    [Fact]
    public async Task Analyze_MajorCooldowns_FixtureCarriesNoOrbSnapshotsSoOrbDerivedReadingsAreUnknown()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.MajorCooldownAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Windows.ShouldAllBe(window => window.OrbsAtActivation == null);
        analyzer.Windows.ShouldAllBe(window => window.OvercapsInside == null);
    }

    [Fact]
    public async Task Analyze_MajorCooldowns_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.MajorCooldownAnalyzers.ShouldHaveSingleItem();
        entry.Pull.MajorCooldownAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).MajorCooldownAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_Downtime_AccountsForEveryMillisecondOfTheMeasuredSpan()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.DowntimeAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.MeasuredSpanMs.ShouldBeGreaterThan(0);
        analyzer.ActiveMs.ShouldBeGreaterThan(0);
        (analyzer.ActiveMs + analyzer.DowntimeMs).ShouldBe(analyzer.MeasuredSpanMs);
        analyzer.ActiveRatio.ShouldBeInRange(0, 1);
    }

    [Fact]
    public async Task Analyze_Downtime_SurfacesTheLongestGapsFirstAndCountsThemAll()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.DowntimeAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.Gaps.Count.ShouldBeLessThanOrEqualTo(DowntimeAnalyzer.TopGapLimit);
        analyzer.Gaps.Count.ShouldBeLessThanOrEqualTo(analyzer.GapCount);
        analyzer.Gaps.Select(gap => gap.DurationMs).ShouldBeInOrder(SortDirection.Descending);
        analyzer.Gaps.ShouldAllBe(gap => gap.DurationMs >= DowntimeAnalyzer.MinimumGapMs);
        analyzer.Gaps.ShouldNotBeEmpty();
        analyzer.LongestGapMs.ShouldBe(analyzer.Gaps[0].DurationMs);
        analyzer.DowntimeMs.ShouldBeGreaterThanOrEqualTo(analyzer.Gaps.Sum(gap => gap.DurationMs));
    }

    [Fact]
    public async Task Analyze_Downtime_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.DowntimeAnalyzers.ShouldHaveSingleItem();
        entry.Pull.DowntimeAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).DowntimeAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_WintersEmbraceUplift_AccountsTheAmplifierAcrossEveryWindow()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var tracker = parser.GetModule<WintersEmbraceUpliftTracker>().ShouldNotBeNull();
        tracker.TotalBonusDamage.ShouldBe(11_804_763);
        tracker.AmplifiedEventCount.ShouldBe(1_740);
        tracker.TotalDamage.ShouldBe(244_068_746);
        tracker.UpliftShare.ShouldBe(0.048, 0.001);
    }

    [Fact]
    public async Task Analyze_WintersEmbraceUplift_SplitsTheBonusAcrossTheSpellsThatEarnedIt()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var tracker = parser.GetModule<WintersEmbraceUpliftTracker>().ShouldNotBeNull();
        tracker.BySpell.Sum(uplift => uplift.BonusDamage).ShouldBe(tracker.TotalBonusDamage);
        tracker.BySpell.Select(uplift => uplift.BonusDamage).ShouldBeInOrder(SortDirection.Descending);
        tracker.BySpell.ShouldAllBe(uplift => uplift.Name.Length > 0);
        tracker.BySpell.ShouldNotContain(uplift => uplift.AbilityId == Spells.BurstingIce.FSLID.Value);
        tracker.BySpell.ShouldNotContain(uplift => uplift.AbilityId == Spells.BurstingIceDamage.FSLID.Value);
        tracker.BySpell[0].Name.ShouldBe(Spells.IceComet.Name);
    }

    [Fact]
    public async Task Analyze_FrostweaverWrath_IsActiveForTheFixturePlayerAndBalancesEveryProc()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.GetModule<FrostweaverWrathAnalyzer>().ShouldNotBeNull();
        analyzer.ProcsGained.ShouldBe(111);
        analyzer.ProcsConsumed.ShouldBe(109);
        analyzer.ProcsExpired.ShouldBe(2);
        analyzer.ProcsOverwritten.ShouldBe(0);
        (analyzer.ProcsConsumed + analyzer.ProcsExpired + analyzer.ProcsOverwritten)
            .ShouldBe(analyzer.ProcsGained);
        analyzer.UtilisationShare.ShouldBe(0.982, 0.001);
    }

    [Fact]
    public async Task Analyze_ShouldCollectStatisticsForTheUpliftAndProcModules()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        var componentTypes = result.Statistics.Select(entry => entry.ComponentType).ToList();
        componentTypes.ShouldContain(typeof(WintersEmbraceStatistics));
        componentTypes.ShouldContain(typeof(FrostweaversWrathStatistics));
        result.Statistics.ShouldContain(entry => entry.Module is WintersEmbraceUpliftTracker);
        result.Statistics.ShouldContain(entry => entry.Module is FrostweaverWrathAnalyzer);
    }

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeFixtureAsync();

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_WintersEmbrace_WindowCountsAreConsistent()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var analyzer = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SuccessfulWindows.ShouldBe(analyzer.Windows.Count(window => window.Successful));
        analyzer.PartialWindows.ShouldBe(analyzer.Windows.Count(window => window.Partial));
        analyzer.Windows.ShouldAllBe(window => !(window.Successful && window.Partial));
        analyzer.Windows.OfType<AoeEmbraceWindowAnalyzer.AoeWindowEvaluation>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_WintersEmbrace_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeFixtureAsync();

        var entry = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);
        entry.Analyzer.EvaluatedWindows.ShouldBeGreaterThan(0);

        pull.WintersEmbraceWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).WintersEmbraceWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
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
