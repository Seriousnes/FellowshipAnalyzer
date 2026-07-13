using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using XavianAbilities = FellowshipAnalyzer.Heroes.Xavian.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class XavianSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new XavianAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
