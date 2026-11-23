using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

using Xunit;

using AeonaAbilities = FellowshipAnalyzer.Heroes.Aeona.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

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
    public void GlobalCooldown_ComesFromTheSpellRecord()
    {
        var spellbook = new AeonaAbilities().Spellbook().ToList();

        SpellbookAbility Entry(Spell spell) => spellbook.Single(e => e.PrimarySpell.FSLID == spell.FSLID);

        spellbook.ShouldAllBe(e => e.Gcd == null);

        Entry(Spells.Intercession).OnGlobalCooldown.ShouldBeTrue();
        Entry(Spells.RevertMagic).OnGlobalCooldown.ShouldBeTrue();
        Entry(Spells.FleetingHour).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.TimeSkip).OnGlobalCooldown.ShouldBeFalse();
    }

    [Fact]
    public void Registry_NamesTheEffectsTheLogWritesForConvergingTimelinesAndAuraOfDeferredFate()
    {
        Spells.ConvergingTimelines.Id.ShouldBe(3266);
        Spells.AuraOfDeferredFate.Id.ShouldBe(2739);
    }
}
