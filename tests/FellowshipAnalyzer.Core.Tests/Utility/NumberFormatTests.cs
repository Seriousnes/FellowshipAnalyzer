using FellowshipAnalyzer.Core.Utility;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Utility;

public class NumberFormatTests
{
    [Theory]
    [InlineData(950, "950")]
    [InlineData(949.6, "950")]
    [InlineData(4_200, "4.2k")]
    [InlineData(38_400, "38k")]
    [InlineData(1_500_000, "1.5M")]
    public void Compact_Abbreviates_Large_Amounts(double amount, string expected) =>
        NumberFormat.Compact(amount).ShouldBe(expected);

    [Theory]
    [InlineData(0.5, 0, "50%")]
    [InlineData(0.425, 1, "42.5%")]
    [InlineData(0.42, 1, "42%")]
    public void Percent_Drops_Trailing_Zero_Decimals(double fraction, int decimals, string expected) =>
        NumberFormat.Percent(fraction, decimals).ShouldBe(expected);

    [Theory]
    [InlineData(1, 4, "25%")]
    [InlineData(3, 0, "0%")]
    public void Share_Guards_Against_Empty_Whole(double part, double whole, string expected) =>
        NumberFormat.Share(part, whole).ShouldBe(expected);

    [Theory]
    [InlineData(1, "first")]
    [InlineData(5, "fifth")]
    [InlineData(6, "6th")]
    [InlineData(11, "11th")]
    [InlineData(22, "22nd")]
    [InlineData(103, "103rd")]
    public void Ordinal_Formats_Positions(int value, string expected) =>
        NumberFormat.Ordinal(value).ShouldBe(expected);
}
