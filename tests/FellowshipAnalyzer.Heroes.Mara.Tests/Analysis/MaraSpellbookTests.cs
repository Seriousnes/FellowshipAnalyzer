using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using MaraAbilities = FellowshipAnalyzer.Heroes.Mara.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new MaraAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
