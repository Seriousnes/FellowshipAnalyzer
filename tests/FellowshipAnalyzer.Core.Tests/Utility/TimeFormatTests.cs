using FellowshipAnalyzer.Core.Utility;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Utility;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, 0, "0s")]
    [InlineData(3_535, 0, "3s")]
    [InlineData(59_999, 0, "59s")]
    [InlineData(-3_535, 0, "-3s")]
    [InlineData(3_535, 3, "3.535s")]
    [InlineData(60_000, 0, "1:00")]
    [InlineData(754_999, 0, "12:34")]
    [InlineData(61_005, 3, "1:01.005")]
    [InlineData(61_005, 1, "1:01.0")]
    [InlineData(-61_250, 3, "-1:01.250")]
    public void Clock_Uses_Seconds_Below_One_Minute_And_Minutes_After(long milliseconds, int precision, string expected) =>
        TimeFormat.Timestamp(milliseconds, precision).ShouldBe(expected);

    [Theory]
    [InlineData(4_000, 0, "4s")]
    [InlineData(4_000, 1, "4.0s")]
    [InlineData(4_540, 1, "4.5s")]
    [InlineData(4_540, 2, "4.54s")]
    public void Seconds_Shows_Fixed_Decimals(double milliseconds, int precision, string expected) =>
        TimeFormat.Seconds(milliseconds, precision).ShouldBe(expected);

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(5 * 60, "5m ago")]
    [InlineData(3 * 3600, "3h ago")]
    [InlineData(2 * 86_400, "2d ago")]
    public void Age_Formats_Elapsed_Time(int secondsAgo, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        TimeFormat.Age(now.AddSeconds(-secondsAgo), now).ShouldBe(expected);
    }

    [Fact]
    public void Date_Returns_Empty_When_Out_Of_Range() =>
        TimeFormat.Date(double.MaxValue).ShouldBeEmpty();
}
