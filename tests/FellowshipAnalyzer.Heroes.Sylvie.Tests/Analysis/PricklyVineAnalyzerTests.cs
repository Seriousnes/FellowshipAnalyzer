using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class PricklyVineAnalyzerTests
{
    [Fact]
    public async Task OneVineIsAliveForItsModelledLifetime()
    {
        var parser = await Analyze(Cast(PullStart, Spells.PricklyVine));

        var analyzer = parser.PricklyVineAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.VineCasts.ShouldBe(1);
        analyzer.PeakLiveVines.ShouldBe(1);
        analyzer.NoVineMs.ShouldBe(analyzer.PullDurationMs - SylvieKit.PricklyVineDurationMs);
        analyzer.AverageLiveVines.ShouldBe(SylvieKit.PricklyVineDurationMs / (double)analyzer.PullDurationMs, 0.01);
    }

    [Fact]
    public async Task ConcurrentVinesStack()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.PricklyVine),
            Cast(PullStart + 2_000, Spells.PricklyVine),
            Cast(PullStart + 4_000, Spells.PricklyVine));

        var analyzer = parser.PricklyVineAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.PeakLiveVines.ShouldBe(3);
        analyzer.AverageLiveVines.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task NoVinesAtAllReadsAsAWholePullWithoutOne()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Nettlebolt));

        var analyzer = parser.PricklyVineAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.VineCasts.ShouldBe(0);
        analyzer.NoVineShare.ShouldBe(1d, 0.001);
        analyzer.AverageLiveVines.ShouldBe(0d);
    }
}
