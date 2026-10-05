using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;
using XavianAbilities = FellowshipAnalyzer.Heroes.Xavian.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class XavianSpellbookTests
{
    private static readonly List<SpellbookAbility> Spellbook = [.. new XavianAbilities().Spellbook()];

    private static SpellbookAbility Entry(Core.Common.Spells.Spell spell) =>
        Spellbook.Single(entry => entry.PrimarySpell.FSLID == spell.FSLID);

    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        Assert.NotEmpty(Spellbook);
        Assert.DoesNotContain(Spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void BrilliantFlash_IsInTheSpellbookOnTheGcd() =>
        Entry(Spells.BrilliantFlash).Gcd.ShouldNotBeNull();

    [Fact]
    public void AbilitiesUsableDuringTheGcd_DoNotTriggerIt()
    {
        Entry(Spells.SolarBlades).Gcd.ShouldBeNull();
        Entry(Spells.ShiningHalo).Gcd.ShouldBeNull();
        Entry(Spells.SkyCrash).Gcd.ShouldBeNull();
        Entry(Spells.OmegaReprieval).Gcd.ShouldBeNull();
        Entry(Spells.DecreeOfTheSun).Gcd.ShouldBeNull();
    }

    [Fact]
    public void InterruptAndTaunt_DoNotScaleWithHaste()
    {
        Entry(Spells.Interrupt).CooldownReducedByHaste.ShouldBeFalse();
        Entry(Spells.Taunt).CooldownReducedByHaste.ShouldBeFalse();
    }

    [Fact]
    public void SkyCrash_HasTwoCharges() => Entry(Spells.SkyCrash).Charges.ShouldBe(2);
}
