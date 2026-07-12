using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using HelenaAbilities = FellowshipAnalyzer.Heroes.Helena.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class HelenaSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new HelenaAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
