using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class ToughnessBandAnalyzerTests
{
    [Fact]
    public async Task EachSample_HoldsItsBandUntilTheNextOne()
    {
        var analyzer = await Analyze(
            ToughnessSample(PullStart, 1_000),
            ToughnessSample(PullStart + 10_000, 400),
            ToughnessSample(PullStart + 20_000, 400));

        analyzer.MsIn(ToughnessBand.Level4).ShouldBe(10_000);
        analyzer.MsIn(ToughnessBand.Level2).ShouldBe(10_000 + (PullEnd - (PullStart + 20_000)));
    }

    [Fact]
    public async Task BandShare_IsWeightedByTimeNotBySampleCount()
    {
        var events = new List<Event> { ToughnessSample(PullStart, 1_000) };
        for (var i = 1; i <= 20; i++)
            events.Add(ToughnessSample(PullStart + 30_000 + (i * 10), 100));
        events.Add(ToughnessSample(PullStart + 31_000, 1_000));

        var analyzer = await Analyze([.. events]);

        analyzer.SampleCount.ShouldBe(22);
        analyzer.TopBandShare.ShouldBeGreaterThan(0.98);
        analyzer.ShareIn(ToughnessBand.Level1).ShouldBeLessThan(0.02);
    }

    [Fact]
    public async Task ASampleBeforeThePullOpened_IsNeverSeen()
    {
        var analyzer = await Analyze(
            ToughnessSample(100, 1_000),
            ToughnessSample(PullStart + 10_000, 100));

        analyzer.SampleCount.ShouldBe(1);
        analyzer.MsIn(ToughnessBand.Level4).ShouldBe(0);
        analyzer.MsIn(ToughnessBand.Level1).ShouldBe(PullEnd - PullStart - 10_000);
    }

    [Fact]
    public async Task ASampleAfterThePullEnded_DoesNotStretchMeasurementPastTheBoundary()
    {
        var analyzer = await Analyze(
            ToughnessSample(PullStart, 1_000),
            ToughnessSample(PullEnd + 5_000, 100));

        analyzer.MeasuredMs.ShouldBe(PullEnd - PullStart);
        analyzer.MsIn(ToughnessBand.Level4).ShouldBe(PullEnd - PullStart);
        analyzer.MsIn(ToughnessBand.Level1).ShouldBe(0);
    }

    [Fact]
    public async Task TheLastSample_HoldsItsBandToThePullEnd()
    {
        var analyzer = await Analyze(ToughnessSample(PullStart, 1_000));

        analyzer.MeasuredMs.ShouldBe(PullEnd - PullStart);
        analyzer.TopBandShare.ShouldBe(1d);
    }

    [Theory]
    [InlineData(0, ToughnessBand.Depleted)]
    [InlineData(240, ToughnessBand.Level1)]
    [InlineData(250, ToughnessBand.Level2)]
    [InlineData(500, ToughnessBand.Level3)]
    [InlineData(750, ToughnessBand.Level4)]
    [InlineData(1_000, ToughnessBand.Level4)]
    public async Task BandMembership_FollowsTheSeasonThreeThresholds(int toughness, ToughnessBand expected)
    {
        var analyzer = await Analyze(ToughnessSample(PullStart, toughness));

        analyzer.ShareIn(expected).ShouldBe(1d);
    }

    [Fact]
    public async Task BandMembership_IsResolvedAgainstEachSamplesOwnMaximum()
    {
        var analyzer = await Analyze(
            ToughnessSample(PullStart, 800, toughnessMax: 1_000),
            ToughnessSample(PullStart + 10_000, 800, toughnessMax: 2_000));

        analyzer.MsIn(ToughnessBand.Level4).ShouldBe(10_000);
        analyzer.MsIn(ToughnessBand.Level2).ShouldBe(PullEnd - PullStart - 10_000);
    }

    [Fact]
    public async Task AverageDamageReduction_IsTheTimeWeightedMeanOfTheBands()
    {
        var analyzer = await Analyze(
            ToughnessSample(PullStart, 1_000),
            ToughnessSample(PullStart + 30_000, 600));

        analyzer.AverageDamageReduction.ShouldBe((0.35 * 0.5) + (0.25 * 0.5), 0.001);
    }

    [Fact]
    public async Task AtMaximum_TracksOnlyTimeWithAFullBar()
    {
        var analyzer = await Analyze(
            ToughnessSample(PullStart, 1_000),
            ToughnessSample(PullStart + 15_000, 999));

        analyzer.AtMaximumMs.ShouldBe(15_000);
        analyzer.AtMaximumShare.ShouldBe(15_000 / (double)(PullEnd - PullStart), 0.001);
    }

    [Fact]
    public async Task APullWithNoToughnessSnapshot_ReadsZeroRatherThanThrowing()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.ShieldSlam));

        analyzer.SampleCount.ShouldBe(0);
        analyzer.MeasuredMs.ShouldBe(0);
        analyzer.TopBandShare.ShouldBe(0);
        analyzer.AverageDamageReduction.ShouldBe(0);
    }

    private static async Task<ToughnessBandAnalyzer> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.ToughnessBandAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
