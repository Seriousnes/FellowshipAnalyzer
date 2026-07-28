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
    /// Sledgehammer is the talent splash on Skull Crusher and lands under its own ability id, so it
    /// only reaches the spellbook through Skull Crusher's additional spells. It is absent from Tariq's
    /// Kit block in the hero data, which is why it needs an <c>overrides.json</c> scope entry.
    /// </summary>
    [Fact]
    public void SkullCrusher_CarriesItsTalentSplashAndLightningRider()
    {
        var entry = new TariqAbilities().Spellbook()
            .Where(spell => spell.PrimarySpell.FSLID == TariqSpells.SkullCrusher.FSLID)
            .ShouldHaveSingleItem();

        var additional = entry.AdditionalSpells.ShouldNotBeNull().Select(spell => spell.FSLID).ToList();

        additional.ShouldContain(TariqSpells.Sledgehammer.FSLID);
        additional.ShouldContain(TariqSpells.SkullCrusherLightningDamage.FSLID);
    }

    /// <summary>
    /// <c>CooldownReducedByHaste</c> is hand-written and nothing generates it, so it drifts from the game
    /// data silently: unset, <c>SpellUsable</c> models the unhasted cooldown and every readiness metric
    /// built on it reads late. These five are the abilities the season 3 hero data marks
    /// <c>SpellCDHasted: true</c> in Tariq's <c>Kit</c> block, and no others.
    /// </summary>
    [Fact]
    public void OnlyTheAbilitiesTheHeroDataMarksHasted_HaveHastedCooldowns()
    {
        var hasted = new TariqAbilities().Spellbook()
            .Where(spell => spell.CooldownReducedByHaste)
            .Select(spell => (int)spell.PrimarySpell.FSLID)
            .ToList();

        int[] expected =
        [
            TariqSpells.LeapSmash.FSLID,
            TariqSpells.CullingStrike.FSLID,
            TariqSpells.ChainLightning.FSLID,
            TariqSpells.HeavyStrike.FSLID,
            TariqSpells.FaceBreaker.FSLID,
        ];

        hasted.ShouldBe(expected, ignoreOrder: true);
    }
}
