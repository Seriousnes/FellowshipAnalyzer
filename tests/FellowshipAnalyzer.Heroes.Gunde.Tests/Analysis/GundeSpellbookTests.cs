using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using GundeAbilities = FellowshipAnalyzer.Heroes.Gunde.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class GundeSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new GundeAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
