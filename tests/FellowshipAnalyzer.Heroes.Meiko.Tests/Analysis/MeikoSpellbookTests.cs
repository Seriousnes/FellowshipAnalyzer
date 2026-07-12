using FellowshipAnalyzer.Core.Analysis;

using Xunit;

using MeikoAbilities = FellowshipAnalyzer.Heroes.Meiko.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class MeikoSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new MeikoAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }
}
