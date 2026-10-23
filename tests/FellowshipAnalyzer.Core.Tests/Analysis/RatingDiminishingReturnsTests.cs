using FellowshipAnalyzer.Core.Analysis;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Pins <see cref="StatTracker.RatingToPercentage"/> to SimFell's <c>StatDiminishingReturns</c>: the bracket
/// edges 10 / 15 / 20 / 25 are points of the converted percentage, and the penalties 0.98 / 0.96 / 0.94 /
/// 0.92 compound.
/// </summary>
public sealed class RatingDiminishingReturnsTests
{
    [Theory]
    [InlineData(62.5, 0.10)]
    [InlineData(94.387755102, 0.15)]
    [InlineData(127.604166667, 0.20)]
    [InlineData(162.940766, 0.25)]
    public void BracketEdges_ConvertToTheEdgePercentage(double rating, double percentage) =>
        Assert.Equal(percentage, StatTracker.RatingToPercentage(rating), precision: 6);

    /// <summary>
    /// 200 rating is 32% raw. The first four brackets take 26.0705% raw to reach 25%, and the 5.9295% raw
    /// left converts at <c>0.98 × 0.96 × 0.94 × 0.92</c>: 25 + 5.9295 × 0.8136 = 29.8242%.
    /// </summary>
    [Fact]
    public void TwoHundredRating_ConvertsTo29Point8242Percent() =>
        Assert.Equal(0.298242, StatTracker.RatingToPercentage(200), precision: 6);

    /// <summary>Inside the 15 to 20 bracket one rating converts at <c>0.16 × 0.98 × 0.96</c> percentage points.</summary>
    [Fact]
    public void ThirdBracket_ConvertsAtTheCompoundedPenalty() =>
        Assert.Equal(0.0016 * 0.98 * 0.96, StatTracker.RatingToPercentage(111) - StatTracker.RatingToPercentage(110), precision: 10);

    [Fact]
    public void RatingBelowTheFirstEdge_ConvertsUndiminished() =>
        Assert.Equal(0.08, StatTracker.RatingToPercentage(50), precision: 10);
}
