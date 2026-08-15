using FellowshipAnalyzer.Api.Core;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class FellowshipLogsApiHandlerTests
{
    [Theory]
    [InlineData("ardeos", "ardeos")]
    [InlineData("Ardeos", "ardeos")]
    [InlineData("RIME", "rime")]
    public void SanitizeHero_NormalisesToLowercase(string hero, string expected)
    {
        FellowshipLogsApiHandler.SanitizeHero(hero).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ardeos-1")]
    [InlineData("ardeos 2")]
    [InlineData("a\r\nEvents hero=forged cache=MISS")]
    [InlineData("aBc123XY")]
    public void SanitizeHero_ReducesAnythingElseToUnknown(string? hero)
    {
        FellowshipLogsApiHandler.SanitizeHero(hero).ShouldBe("unknown");
    }

    [Fact]
    public void SanitizeHero_ReducesOverlongHeroToUnknown()
    {
        FellowshipLogsApiHandler.SanitizeHero(new string('a', 33)).ShouldBe("unknown");
    }
}
