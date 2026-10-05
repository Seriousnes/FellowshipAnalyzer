using FellowshipAnalyzer.Heroes.Meiko.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

using static FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis.MeikoAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class SelfBuffAnalyzerTests
{
    [Fact]
    public async Task SpiritedStrikesUptime_IsTheActiveShareOfThePull()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.SpiritedStrikes),
            RemoveBuff(PullStart + 30_000, Spells.SpiritedStrikes));

        var analyzer = (SpiritedStrikesAnalyzer)parser.SpiritedStrikesAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.ActiveMs.ShouldBe(30_000);
        analyzer.InactiveMs.ShouldBe(30_000);
        analyzer.Uptime.ShouldBe(0.5, 0.001);
        analyzer.Drops.ShouldBe(1);
    }

    [Fact]
    public async Task ARefreshKeepsSpiritedStrikesUpWithoutADrop()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.SpiritedStrikes),
            RefreshBuff(PullStart + 20_000, Spells.SpiritedStrikes),
            RefreshBuff(PullStart + 40_000, Spells.SpiritedStrikes));

        var analyzer = (SpiritedStrikesAnalyzer)parser.SpiritedStrikesAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Uptime.ShouldBe(1, 0.001);
        analyzer.Drops.ShouldBe(0);
    }

    [Fact]
    public async Task ABuffAppliedBeforeThePull_CountsFromThePullStart()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.SpiritedVortexBuffIcon),
            RemoveBuff(PullStart + 15_000, Spells.SpiritedVortexBuffIcon));

        var analyzer = (SpiritedVortexAnalyzer)parser.SpiritedVortexAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.ActiveMs.ShouldBe(15_000);
        analyzer.Uptime.ShouldBe(0.25, 0.001);
    }

    [Fact]
    public async Task APullWithoutTheBuff_ReadsZeroUptime()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.EarthFist));

        var analyzer = (SpiritedVortexAnalyzer)parser.SpiritedVortexAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.ActiveMs.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0);
        analyzer.Drops.ShouldBe(0);
    }
}
