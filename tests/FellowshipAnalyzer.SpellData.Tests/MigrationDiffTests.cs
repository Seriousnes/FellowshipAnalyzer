using FellowshipAnalyzer.SpellData;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class MigrationDiffTests
{
    [Theory]
    [InlineData("items", "VoidbringerTouch", 155)]
    public void CrossHeroMove_IsReproducedByGuid(string scope, string member, int guid) =>
        MergeEngine.Run(MergeInputs.Load()).Spells
            .ShouldContain(s => string.Equals(s.Scope, scope, StringComparison.OrdinalIgnoreCase)
                                && s.Member == member && s.FSLID.Value == guid);
}
