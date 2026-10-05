using Shouldly;
using Xunit;
using static FellowshipAnalyzer.Core.Utility.Inflection;

namespace FellowshipAnalyzer.Core.Tests.Utility;

public class InflectionTests
{
    [Theory]
    [InlineData(1, "charge")]
    [InlineData(0, "charges")]
    [InlineData(3, "charges")]
    public void Plural_Selects_The_Form_That_Agrees(long value, string expected) =>
        Plural(value, "charge", "charges").ShouldBe(expected);
}
