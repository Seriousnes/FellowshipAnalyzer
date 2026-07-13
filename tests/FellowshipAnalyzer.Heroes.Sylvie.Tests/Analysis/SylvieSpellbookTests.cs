using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using SylvieAbilities = FellowshipAnalyzer.Heroes.Sylvie.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class SylvieSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new SylvieAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
