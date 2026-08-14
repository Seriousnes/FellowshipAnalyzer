using FellowshipAnalyzer.Api.Core;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class UsageApiHandlerTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/report/aBc123XY/42/7")]
    [InlineData("/report/a:aBc123XY/42/7")]
    [InlineData("/character/1234")]
    public void SanitizePath_AcceptsApplicationRoutes(string path)
    {
        UsageApiHandler.SanitizePath(path).ShouldBe(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("report/aBc123XY")]
    [InlineData("/report/<script>")]
    [InlineData("/report/a b")]
    [InlineData("/report/a\r\nUsage hero forged /")]
    public void SanitizePath_RejectsAnythingElse(string? path)
    {
        UsageApiHandler.SanitizePath(path).ShouldBeNull();
    }

    [Fact]
    public void SanitizePath_RejectsOverlongPath()
    {
        UsageApiHandler.SanitizePath("/" + new string('a', 200)).ShouldBeNull();
    }

    [Theory]
    [InlineData("ardeos", "ardeos")]
    [InlineData("Ardeos", "ardeos")]
    [InlineData("RIME", "rime")]
    public void SanitizeHero_NormalisesToLowercase(string hero, string expected)
    {
        UsageApiHandler.SanitizeHero(hero).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ardeos-1")]
    [InlineData("ardeos 2")]
    [InlineData("a\r\nUsage hero forged")]
    public void SanitizeHero_RejectsAnythingElse(string? hero)
    {
        UsageApiHandler.SanitizeHero(hero).ShouldBeNull();
    }

    [Fact]
    public void SanitizeHero_RejectsOverlongHero()
    {
        UsageApiHandler.SanitizeHero(new string('a', 33)).ShouldBeNull();
    }
}
