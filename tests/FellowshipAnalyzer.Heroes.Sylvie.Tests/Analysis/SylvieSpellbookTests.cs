using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;
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

    [Fact]
    public void BothHalvesOfBlueyAreInTheSpellbook()
    {
        var spellbook = new SylvieAbilities().Spellbook().ToList();

        spellbook.ShouldContain(entry => entry.PrimarySpell.FSLID == Spells.FluttercallProtect.FSLID);
        spellbook.ShouldContain(entry => entry.PrimarySpell.FSLID == Spells.FluttercallEmbrace.FSLID);
    }

    [Fact]
    public void EveryDefensiveEntryIsMarkedDefensive()
    {
        var spellbook = new SylvieAbilities().Spellbook().ToList();

        foreach (var entry in spellbook.Where(entry => entry.Category == SpellCategory.Defensive))
            entry.IsDefensive.ShouldBeTrue($"{entry.PrimarySpell.Name} is filed as Defensive but not marked IsDefensive");
    }

    [Fact]
    public void VineDamageIsAttributedToPricklyVine()
    {
        var vine = new SylvieAbilities().Spellbook()
            .Single(entry => entry.PrimarySpell.FSLID == Spells.PricklyVine.FSLID);

        vine.AdditionalSpells.ShouldNotBeNull()
            .ShouldContain(spell => spell.FSLID == Spells.PricklyVineDamage.FSLID);
    }

    [Fact]
    public void ManaCostsCoverEveryCostedAbilityInTheSpellbook()
    {
        var costed = new[]
        {
            Spells.Nettlebolt.FSLID,
            Spells.PricklyVine.FSLID,
            Spells.LifePetal.FSLID,
            Spells.HeartBloom.FSLID,
            Spells.IronleafWard.FSLID,
            Spells.CureAilment.FSLID,
            Spells.EnfeeblingRootsap.FSLID,
            Spells.SafeHaven.FSLID,
            Spells.HiddenTrail.FSLID,
            Spells.FluttercallHeal.FSLID,
            Spells.FluttercallRestoreLife.FSLID,
            Spells.FluttercallProtect.FSLID,
            Spells.FluttercallEmbrace.FSLID,
        };

        foreach (var id in costed)
            SylvieKit.ManaCost(id).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void KitConstantsMatchTheSeasonThreeGameData()
    {
        SylvieKit.HeartBloomAccumulatedShare.ShouldBe(0.12, 0.0001);
        SylvieKit.HeartBloomTickIntervalMs.ShouldBe(2_000);
        SylvieKit.HeartBloomDurationMs.ShouldBe(16_000);
        SylvieKit.BlueyAllyHealScaler.ShouldBe(2.0, 0.0001);
        SylvieKit.BlueySelfHealScaler.ShouldBe(1.5, 0.0001);
        SylvieKit.EmbraceManaCostScaler.ShouldBe(0.8, 0.0001);
        SylvieKit.FlutterflyHealPerVine.ShouldBe(0.03, 0.0001);
        SylvieKit.SafeHavenHealingTakenMultiplier.ShouldBe(1.4, 0.0001);
        SylvieKit.PricklyVineDurationMs.ShouldBe(27_000);
        SylvieKit.LifePetalCharges.ShouldBe(2);
        SylvieKit.PinkButterflies.ShouldBe(4);
    }
}
