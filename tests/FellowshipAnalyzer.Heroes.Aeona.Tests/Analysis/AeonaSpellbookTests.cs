using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using AeonaAbilities = FellowshipAnalyzer.Heroes.Aeona.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class AeonaSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new AeonaAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
