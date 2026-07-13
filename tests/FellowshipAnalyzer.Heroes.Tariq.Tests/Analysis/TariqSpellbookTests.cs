using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using TariqAbilities = FellowshipAnalyzer.Heroes.Tariq.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class TariqSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new TariqAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
