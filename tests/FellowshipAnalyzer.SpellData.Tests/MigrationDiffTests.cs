using FellowshipAnalyzer.SpellData;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class MigrationDiffTests
{
    [Theory]
    [InlineData("Rime")]
    [InlineData("Elarion")]
    [InlineData("Aeona")]
    [InlineData("Ardeos")]
    [InlineData("Helena")]
    [InlineData("Mara")]
    [InlineData("Meiko")]
    [InlineData("Sylvie")]
    [InlineData("Tariq")]
    [InlineData("Vigour")]
    [InlineData("Xavian")]
    public void EveryHandWrittenMember_IsReproduced(string hero)
    {
        var handWritten = HandWrittenRegistrySnapshot.For(hero);
        var merged = MergeEngine.Run(MergeInputs.Load()).Spells
            .Where(s => string.Equals(s.Scope, hero, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var pairs = merged.Select(s => (s.Member, s.FSLID.Value)).ToHashSet();

        var missing = handWritten
            .Where(m => !pairs.Contains((m.Member, m.Guid)))
            .Select(m => $"{m.Member} (guid {m.Guid})")
            .ToList();

        missing.ShouldBeEmpty($"{hero}: unreproduced members — add overrides:\n  {string.Join("\n  ", missing)}");
    }

    [Theory]
    [InlineData("items", "VoidbringerTouch", 155)]
    public void CrossHeroMove_IsReproducedByGuid(string scope, string member, int guid) =>
        MergeEngine.Run(MergeInputs.Load()).Spells
            .ShouldContain(s => string.Equals(s.Scope, scope, StringComparison.OrdinalIgnoreCase)
                                && s.Member == member && s.FSLID.Value == guid);
}
