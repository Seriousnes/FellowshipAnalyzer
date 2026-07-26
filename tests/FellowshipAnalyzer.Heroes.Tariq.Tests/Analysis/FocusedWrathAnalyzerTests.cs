using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class FocusedWrathAnalyzerTests
{
    [Fact]
    public async Task Analyze_FocusedWrath_MeasuresLatencyToTheFirstSpenderInsideTheBuff()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_300, TariqSpells.SkullCrusher.FSLID),
            Cast(2_000, TariqSpells.HammerStorm.FSLID),
            Cast(3_000, TariqSpells.CullingStrike.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.Timestamp.ShouldBe(1_000);
        cast.LatencyToSpenderMs.ShouldBe(300);
        cast.SpendersInBuff.ShouldBe(3);

        analyzer.CastCount.ShouldBe(1);
        analyzer.UnpairedCasts.ShouldBe(0);
        analyzer.AverageLatencyMs.ShouldBe(300);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_LeavesACastWithNoSpenderInsideTheBuffUnpaired()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_000 + FocusedWrathAnalyzer.BuffDurationMs + 1, TariqSpells.SkullCrusher.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.LatencyToSpenderMs.ShouldBeNull();
        cast.SpendersInBuff.ShouldBe(0);

        analyzer.UnpairedCasts.ShouldBe(1);
        analyzer.AverageLatencyMs.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_FocusedWrath_PairsASpenderOnTheLastMillisecondOfTheBuff()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_000 + FocusedWrathAnalyzer.BuffDurationMs, TariqSpells.HammerStorm.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldHaveSingleItem().LatencyToSpenderMs.ShouldBe(FocusedWrathAnalyzer.BuffDurationMs);
        analyzer.UnpairedCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_PairsASpenderLoggedAtTheSameMillisecondAsTheCast()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_000, TariqSpells.SkullCrusher.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.LatencyToSpenderMs.ShouldBe(0);
        cast.SpendersInBuff.ShouldBe(1);
        analyzer.UnpairedCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_IgnoresBuffApplicationsThatNoCastProduced()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            ThunderCallWindowAnalyzerTests.Apply(1_000, TariqSpells.FocusedWrathSelfBuff.FSLID),
            Cast(1_200, TariqSpells.SkullCrusher.FSLID),
            ThunderCallWindowAnalyzerTests.Apply(2_000, TariqSpells.FocusedWrathSelfBuff.FSLID),
            Cast(2_400, TariqSpells.HammerStorm.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBeEmpty();
        analyzer.CastCount.ShouldBe(0);
        analyzer.UnpairedCasts.ShouldBe(0);
        analyzer.AverageLatencyMs.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_FocusedWrath_AveragesLatencyOverThePairedCastsOnly()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_500, TariqSpells.SkullCrusher.FSLID),
            Cast(95_000, TariqSpells.FocusedWrath.FSLID),
            Cast(190_000, TariqSpells.FocusedWrath.FSLID),
            Cast(191_500, TariqSpells.CullingStrike.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(3);
        analyzer.UnpairedCasts.ShouldBe(1);
        analyzer.Casts.Select(cast => cast.LatencyToSpenderMs).ShouldBe([500, null, 1_500]);
        analyzer.AverageLatencyMs.ShouldBe(1_000);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_ExposesPerPullReadPaths()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Cast(1_500, TariqSpells.SkullCrusher.FSLID),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent Cast(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, abilityId);
}
