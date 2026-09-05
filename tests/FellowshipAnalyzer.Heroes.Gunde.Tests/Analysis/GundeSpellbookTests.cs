using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

using GundeAbilities = FellowshipAnalyzer.Heroes.Gunde.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class GundeSpellbookTests
{
    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new GundeAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void HeartSplitter_HasTheExsanguinateBonusDamageEffect()
    {
        var heartSplitter = Entry(Spells.HeartSplitter.FSLID);

        heartSplitter.AdditionalSpells.ShouldNotBeNull();
        heartSplitter.AdditionalSpells.Select(s => s.FSLID.Value)
            .ShouldContain(Spells.HeartSplitterDotBonusDamage.FSLID.Value);
    }

    [Fact]
    public void OnlyTheSourceHastedAbilities_ScaleTheirCooldownWithHaste()
    {
        var hasted = new GundeAbilities().Spellbook()
            .Where(e => e.CooldownReducedByHaste)
            .Select(e => e.PrimarySpell.FSLID.Value)
            .ToList();

        hasted.ShouldBe(
            [
                Spells.GrimCarve.FSLID.Value,
                Spells.ReaverEdge.FSLID.Value,
                Spells.BloodArc.FSLID.Value,
                Spells.HeartSplitter.FSLID.Value,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void HastedAbilities_ShortenTheirCooldownAsHasteRises()
    {
        var grimCarve = Entry(Spells.GrimCarve.FSLID);
        var slaughter = Entry(Spells.Slaughter.FSLID);

        grimCarve.GetCooldown().ShouldBe(15d);
        grimCarve.GetCooldown(haste: 0.5).ShouldBe(10d, 0.0001);
        grimCarve.GetCooldown(haste: 0.25).ShouldBe(12d, 0.0001);

        slaughter.GetCooldown().ShouldBe(30d);
        slaughter.GetCooldown(haste: 0.5).ShouldBe(30d);
    }

    [Fact]
    public void CuratedCooldowns_ReachTheSpellbook()
    {
        Entry(Spells.OwedInBlood.FSLID).GetCooldown().ShouldBe(3);
        Entry(Spells.Warbound.FSLID).GetCooldown().ShouldBe(8);
    }

    private static SpellbookAbility Entry(int fslid) =>
        new GundeAbilities().Spellbook().Single(e => e.PrimarySpell.FSLID.Value == fslid);
}
