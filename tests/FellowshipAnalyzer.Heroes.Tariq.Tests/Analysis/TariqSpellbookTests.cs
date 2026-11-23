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

    [Fact]
    public void NoEntry_DeclaresAGlobalCooldown()
    {
        foreach (var entry in new TariqAbilities().Spellbook())
            entry.Gcd.ShouldBeNull();
    }

    [Fact]
    public void CullingStrikeIsOffTheGlobalCooldownAndSkullCrusherIsOn()
    {
        var spellbook = new TariqAbilities().Spellbook().ToList();

        spellbook.Single(entry => entry.PrimarySpell.FSLID == TariqSpells.CullingStrike.FSLID)
            .OnGlobalCooldown.ShouldBeFalse();
        spellbook.Single(entry => entry.PrimarySpell.FSLID == TariqSpells.SkullCrusher.FSLID)
            .OnGlobalCooldown.ShouldBeTrue();
        TariqSpells.SkullCrusher.GlobalCooldown.ShouldBe(1);
        TariqSpells.SkullCrusher.GlobalCooldownScalesWithHaste.ShouldBe(true);
        TariqSpells.CullingStrike.GlobalCooldown.ShouldBeNull();
        TariqSpells.WildSwing.GlobalCooldown.ShouldBe(1.5);
        TariqSpells.WildSwing.Cooldown.ShouldBe(1.5);
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
    /// <c>CooldownHaste</c> defaults to the export's <c>Spell.CooldownHaste</c>. No Tariq entry departs
    /// from the export, so every entry's cooldown Haste scaling matches it.
    /// </summary>
    [Fact]
    public void EveryEntry_HasTheCooldownHasteScalingTheExportStates()
    {
        foreach (var spell in new TariqAbilities().Spellbook())
            spell.CooldownHaste.ShouldBe(spell.PrimarySpell.CooldownHaste, spell.PrimarySpell.Name);
    }
}
