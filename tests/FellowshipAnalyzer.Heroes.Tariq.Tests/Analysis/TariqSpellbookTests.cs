using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

using TariqAbilities = FellowshipAnalyzer.Heroes.Tariq.Modules.Abilities;
using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

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

    /// <summary>
    /// Sledgehammer is the talent splash on Skull Crusher with its own ability id, so it
    /// only reaches the spellbook through Skull Crusher's additional spells. It is absent from Tariq's
    /// Kit block in the hero data, which is why it needs an <c>overrides.json</c> scope entry.
    /// </summary>
    [Fact]
    public void SkullCrusher_HasItsTalentSplashAndLightningRider()
    {
        var entry = new TariqAbilities().Spellbook()
            .Where(spell => spell.PrimarySpell.FSLID == TariqSpells.SkullCrusher.FSLID)
            .ShouldHaveSingleItem();

        var additional = entry.AdditionalSpells.ShouldNotBeNull().Select(spell => spell.FSLID).ToList();

        additional.ShouldContain(TariqSpells.Sledgehammer.FSLID);
        additional.ShouldContain(TariqSpells.SkullCrusherLightningDamage.FSLID);
    }

    /// <summary>
    /// <c>CooldownReducedByHaste</c> defaults to the export's <c>Spell.CooldownScalesWithHaste</c>. No
    /// Tariq entry departs from the export, so every entry's haste scaling matches it.
    /// </summary>
    [Fact]
    public void EveryEntry_HasTheCooldownHasteScalingTheExportStates()
    {
        foreach (var spell in new TariqAbilities().Spellbook())
            spell.CooldownReducedByHaste.ShouldBe(spell.PrimarySpell.CooldownScalesWithHaste == true, spell.PrimarySpell.Name);
    }
}
