using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

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

    [Fact]
    public void Registry_NamesTheEffectsTheLogWritesForConvergingTimelinesAndAuraOfDeferredFate()
    {
        Spells.ConvergingTimelines.Id.ShouldBe(3266);
        Spells.AuraOfDeferredFate.Id.ShouldBe(2739);
    }
}
