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
    public void NoEntry_DeclaresAGlobalCooldown() =>
        Spellbook.ShouldAllBe(entry => entry.Gcd == null);

    [Fact]
    public void TheGlobalCooldownAbilities_TriggerItFromTheExport()
    {
        Spellbook.Where(entry => entry.OnGlobalCooldown)
            .Select(entry => entry.PrimarySpell.FSLID)
            .ShouldBe(
                [
                    Spells.BrilliantFlash.FSLID,
                    Spells.SunStrike.FSLID,
                    Spells.BrilliantFlare.FSLID,
                    Spells.BlindingSlash.FSLID,
                    Spells.Omnistrike.FSLID,
                ],
                ignoreOrder: true);
    }

    [Fact]
    public void AbilitiesUsableDuringTheGcd_DoNotTriggerIt()
    {
        Entry(Spells.SolarBlades).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.ShiningHalo).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.SkyCrash).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.OmegaReprieval).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.DecreeOfTheSun).OnGlobalCooldown.ShouldBeFalse();
    }

    [Fact]
    public void OnlyHasteScaledCooldowns_AreReducedByHaste()
    {
        Spellbook.Where(entry => entry.CooldownReducedByHaste)
            .Select(entry => entry.PrimarySpell.FSLID)
            .ShouldBe(
                [Spells.BlindingSlash.FSLID, Spells.Omnistrike.FSLID, Spells.SolarBlades.FSLID, Spells.SolarShield.FSLID],
                ignoreOrder: true);
    }

    [Fact]
    public void SkyCrash_HasTwoCharges() => Entry(Spells.SkyCrash).Charges.ShouldBe(2);

    [Fact]
    public void Taunt_HasTwoCharges() => Entry(Spells.Taunt).Charges.ShouldBe(2);

    [Fact]
    public void RupturedDawn_HasAFortyFiveSecondCooldown() => Entry(Spells.RupturedDawn).GetCooldown(0.5).ShouldBe(45);
}
