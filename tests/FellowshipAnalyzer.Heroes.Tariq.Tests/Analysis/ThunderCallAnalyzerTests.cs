using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class ThunderCallAnalyzerTests
{
    private const int PlayerId = 7;
    private const int FightEnd = 300_000;

    private static readonly int ThunderCallBuff = TariqSpells.ThunderCallBuff.FSLID;
    private static readonly int RagingTempestBuff = TariqSpells.RagingTempestPulsatingSingleDamageSelfBuff.FSLID;

    [Fact]
    public async Task Analyze_ThunderCallWindow_RecordsTheWindowItsFuryAtOpenAndTheCastsInside()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.ThunderCall.FSLID, fury: 70),
            Apply(1_100, ThunderCallBuff),
            Cast(2_000, TariqSpells.SkullCrusher.FSLID, fury: 45),
            Cast(3_000, TariqSpells.ChainLightning.FSLID, fury: 20),
            Cast(4_000, TariqSpells.HammerStorm.FSLID, fury: 60),
            Cast(5_000, TariqSpells.CullingStrike.FSLID, fury: 10),
            Remove(22_100, ThunderCallBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.Source.ShouldBe(ThunderCallWindowSource.ThunderCall);
        window.OpenedAt.ShouldBe(1_100);
        window.ClosedAt.ShouldBe(22_100);
        window.DurationMs.ShouldBe(ThunderCallAnalyzer.ThunderCallWindowDurationMs);
        window.ExpectedDurationMs.ShouldBe(ThunderCallAnalyzer.ThunderCallWindowDurationMs);
        window.ClippedByPullEnd.ShouldBeFalse();
        window.OverlapsOtherWindow.ShouldBeFalse();
        window.FuryAtOpen.ShouldBe(70);
        window.SkullCrusherCasts.ShouldBe(1);
        window.HammerStormCasts.ShouldBe(1);
        window.CullingStrikeCasts.ShouldBe(1);
        window.ChainLightningCasts.ShouldBe(1);
        window.SpenderCasts.ShouldBe(3);
        window.CastsInWindow.Select(cast => cast.Timestamp).ShouldBe([2_000, 3_000, 4_000, 5_000]);

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ClippedWindows.ShouldBe(0);
        analyzer.OverlappingWindows.ShouldBe(0);
        analyzer.AverageFuryAtOpen.ShouldBe(70);
        analyzer.TotalSpenderCasts.ShouldBe(3);
        analyzer.TotalChainLightningCasts.ShouldBe(1);
        analyzer.AverageSpendersPerWindow.ShouldBe(3);
    }

    /// <summary>
    /// Raging Tempest's self-buff runs 20s, not the 21s of Thunder Call or of the ability's own
    /// <c>BuffDuration</c>; report <c>a:NcqHDKzamL7n6YFv</c> shows 12 of 13 windows at exactly 20.0s.
    /// The fixture is built at that length so the expected duration cannot silently read from the
    /// Thunder Call constant.
    /// </summary>
    [Fact]
    public async Task Analyze_ThunderCallWindow_CountsARagingTempestWindowUnderItsOwnSource()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.RagingTempest.FSLID, fury: 90),
            Apply(1_100, RagingTempestBuff),
            Cast(2_000, TariqSpells.HammerStorm.FSLID, fury: 40),
            Remove(21_100, RagingTempestBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.Source.ShouldBe(ThunderCallWindowSource.RagingTempest);
        window.DurationMs.ShouldBe(ThunderCallAnalyzer.RagingTempestWindowDurationMs);
        window.ExpectedDurationMs.ShouldBe(ThunderCallAnalyzer.RagingTempestWindowDurationMs);
        window.FuryAtOpen.ShouldBe(90);
        window.HammerStormCasts.ShouldBe(1);
        window.SpenderCasts.ShouldBe(1);
    }

    [Fact]
    public void ExpectedDuration_DiffersPerWindowSource()
    {
        ThunderCallAnalyzer.ExpectedDurationMs(ThunderCallWindowSource.ThunderCall)
            .ShouldBe(ThunderCallAnalyzer.ThunderCallWindowDurationMs);
        ThunderCallAnalyzer.ExpectedDurationMs(ThunderCallWindowSource.RagingTempest)
            .ShouldBe(ThunderCallAnalyzer.RagingTempestWindowDurationMs);
        ThunderCallAnalyzer.RagingTempestWindowDurationMs
            .ShouldNotBe(ThunderCallAnalyzer.ThunderCallWindowDurationMs);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_ClosesAWindowStillOpenAtPullEndAndFlagsItClipped()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(279_000, TariqSpells.ThunderCall.FSLID, fury: 55),
            Apply(280_000, ThunderCallBuff),
            Cast(281_000, TariqSpells.SkullCrusher.FSLID, fury: 30),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.ClippedByPullEnd.ShouldBeTrue();
        window.ClosedAt.ShouldBe(FightEnd);
        window.DurationMs.ShouldBe(20_000);
        analyzer.ClippedWindows.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_FlagsBothWindowsWhenTheTwoCooldownsOverlap()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Apply(5_000, RagingTempestBuff),
            Remove(22_000, ThunderCallBuff),
            Remove(26_000, RagingTempestBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WindowCount.ShouldBe(2);
        analyzer.OverlappingWindows.ShouldBe(2);
        analyzer.Windows.ShouldAllBe(window => window.OverlapsOtherWindow);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_LeavesBackToBackWindowsUnflagged()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Remove(22_000, ThunderCallBuff),
            Apply(22_000, RagingTempestBuff),
            Remove(43_000, RagingTempestBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WindowCount.ShouldBe(2);
        analyzer.OverlappingWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_IgnoresSpendersAndChainLightningCastOutsideAnyWindow()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(500, TariqSpells.SkullCrusher.FSLID, fury: 80),
            Cast(600, TariqSpells.ChainLightning.FSLID, fury: 70),
            Apply(1_000, ThunderCallBuff),
            Cast(2_000, TariqSpells.SkullCrusher.FSLID, fury: 50),
            Remove(3_000, ThunderCallBuff),
            Cast(4_000, TariqSpells.HammerStorm.FSLID, fury: 20),
            Cast(5_000, TariqSpells.ChainLightning.FSLID, fury: 10),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.SkullCrusherCasts.ShouldBe(1);
        window.HammerStormCasts.ShouldBe(0);
        window.ChainLightningCasts.ShouldBe(0);
        window.CastsInWindow.ShouldHaveSingleItem().Timestamp.ShouldBe(2_000);
        analyzer.TotalSpenderCasts.ShouldBe(1);
        analyzer.TotalChainLightningCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_AttributesNestedCastsToTheMostRecentlyOpenedWindow()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Apply(2_000, RagingTempestBuff),
            Cast(3_000, TariqSpells.SkullCrusher.FSLID, fury: 60),
            Remove(4_000, RagingTempestBuff),
            Cast(5_000, TariqSpells.HammerStorm.FSLID, fury: 55),
            Remove(6_000, ThunderCallBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;

        var thunderCall = analyzer.Windows[0];
        thunderCall.Source.ShouldBe(ThunderCallWindowSource.ThunderCall);
        thunderCall.SkullCrusherCasts.ShouldBe(0);
        thunderCall.HammerStormCasts.ShouldBe(1);

        var ragingTempest = analyzer.Windows[1];
        ragingTempest.Source.ShouldBe(ThunderCallWindowSource.RagingTempest);
        ragingTempest.SkullCrusherCasts.ShouldBe(1);
        ragingTempest.HammerStormCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_LeavesFuryAtOpenUnsetBeforeAnyCastSnapshot()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Remove(22_000, ThunderCallBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Windows.ShouldHaveSingleItem().FuryAtOpen.ShouldBeNull();
        analyzer.AverageFuryAtOpen.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_AveragesFuryOverTheWindowsThatHaveAReading()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Cast(2_000, TariqSpells.HeavyStrike.FSLID, fury: 30),
            Remove(22_000, ThunderCallBuff),
            Apply(50_000, ThunderCallBuff),
            Remove(71_000, ThunderCallBuff),
        ]);

        var analyzer = parser.ThunderCallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Windows[0].FuryAtOpen.ShouldBeNull();
        analyzer.Windows[1].FuryAtOpen.ShouldBe(30);
        analyzer.AverageFuryAtOpen.ShouldBe(30);
    }

    [Fact]
    public async Task Analyze_ThunderCallWindow_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Apply(1_000, ThunderCallBuff),
            Remove(22_000, ThunderCallBuff),
        ]);

        var entry = parser.ThunderCallAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.ThunderCallAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).ThunderCallAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    internal static async Task<(TariqCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var fight = new ReportFight(0, "Boss", 1, true, 0, FightEnd, null, null, null);
        var result = await parser.Analyze(events, playerId: PlayerId, fight: fight);
        return (parser, result);
    }

    /// <summary>
    /// Fury is fabricated at the raw log scale (100x the in-game value), so <c>ResourceNormalizer</c>
    /// divides it down to the percentage the analyzer reads.
    /// </summary>
    internal static CastEvent Cast(int timestamp, int abilityId, int fury) =>
        FuryEconomyAnalyzerTests.Cast(timestamp, abilityId, rawFury: fury * 100, rawMaxFury: 10_000);

    internal static ApplyBuffEvent Apply(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(timestamp, abilityId);

    internal static RemoveBuffEvent Remove(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(timestamp, abilityId);
}
