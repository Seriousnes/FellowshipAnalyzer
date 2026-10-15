using FellowshipAnalyzer.Core.Insights;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Insights;

public class DigestTests
{
    [Theory]
    [InlineData(0, 4, "never")]
    [InlineData(1, 4, "rarely")]
    [InlineData(2, 4, "sometimes")]
    [InlineData(3, 4, "mostly")]
    [InlineData(4, 4, "always")]
    [InlineData(1, 1, "always")]
    public void Often_NamesTheShareShown(int shown, int total, string expected) =>
        Digest.Often(shown, total).ShouldBe(expected);

    [Fact]
    public void Pattern_SharesEachKindOfPullSeparately()
    {
        var pattern = Digest.Pattern("wasted charges", [("boss", false), ("trash pack", true), ("trash pack", false), ("trash pack", true)]);

        pattern.Often.ShouldBe(new Dictionary<string, string> { ["boss"] = "never", ["trash pack"] = "mostly" });
    }
}
