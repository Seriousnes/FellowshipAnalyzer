using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using Shouldly;

using Xunit;

using HelenaAbilities = FellowshipAnalyzer.Heroes.Helena.Modules.Abilities;
using Legendaries = FellowshipAnalyzer.Core.Common.Spells.Helena.Legendaries;
using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class HelenaSpellbookTests
{
    private const int LegendaryQuality = 6;

    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new HelenaAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void ShieldSlamAndShieldsUp_RecoverWithHaste()
    {
        var spellbook = new HelenaAbilities().Spellbook().ToList();

        Entry(spellbook, Spells.ShieldSlam).CooldownReducedByHaste.ShouldBeTrue();
        Entry(spellbook, Spells.ShieldsUp).CooldownReducedByHaste.ShouldBeTrue();
    }

    [Fact]
    public void ShieldSlamAndShieldsUp_TakeTimeRateHasteFromTheSpellData()
    {
        var spellbook = new HelenaAbilities().Spellbook().ToList();

        Spells.ShieldSlam.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
        Spells.ShieldsUp.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
        Entry(spellbook, Spells.ShieldSlam).CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
        Entry(spellbook, Spells.ShieldsUp).CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public void HoldTheLine_ChargeGrantIsIronSentinel()
    {
        var grant = Spells.HoldTheLine.ChargeGrant.ShouldNotBeNull();

        grant.Source.ShouldBe(GrantSource.LegendaryItem);
        grant.Id.ShouldBe(Legendaries.IronSentinel.Id);
        grant.Charges.ShouldBe(2);
        grant.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public void PowerStrikeShieldThrowAndShockwave_RecoverWithHaste()
    {
        var spellbook = new HelenaAbilities().Spellbook().ToList();

        Entry(spellbook, Spells.PowerStrike).CooldownReducedByHaste.ShouldBeTrue();
        Entry(spellbook, Spells.ShieldThrow).CooldownReducedByHaste.ShouldBeTrue();
        Entry(spellbook, Spells.Shockwave).CooldownReducedByHaste.ShouldBeTrue();
    }

    [Fact]
    public void WithoutIronSentinel_HoldTheLineHasOneChargeThatIgnoresHaste()
    {
        var holdTheLine = Entry(new HelenaAbilities().Spellbook().ToList(), Spells.HoldTheLine);

        holdTheLine.Charges.ShouldBe(1);
        holdTheLine.CooldownReducedByHaste.ShouldBeFalse();
    }

    [Fact]
    public async Task IronSentinel_GivesHoldTheLineASecondChargeThatRecoversWithHaste()
    {
        var combatant = Combatant();
        combatant.Gear = [new Item { Id = Legendaries.IronSentinel.Id, Quality = LegendaryQuality }];

        var parser = await AnalyzeAs(
            combatant,
            Cast(PullStart + 1_000, Spells.HoldTheLine),
            Cast(PullStart + 2_000, Spells.HoldTheLine));

        var holdTheLine = parser.GetModule<HelenaAbilities>().ShouldNotBeNull()
            .GetAbility(Spells.HoldTheLine.FSLID).ShouldNotBeNull();

        holdTheLine.Charges.ShouldBe(2);
        holdTheLine.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);

        parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Where(update => update.Ability.Id == Spells.HoldTheLine.FSLID && update.Timestamp == PullStart + 2_000)
            .Select(update => update.UpdateType)
            .ShouldBe([UpdateSpellUsableType.UseCharge]);
    }

    [Fact]
    public async Task RazorCrash_LeavesHoldTheLineAtOneCharge()
    {
        var combatant = Combatant();
        combatant.Gear = [new Item { Id = Legendaries.RazorCrash.Id, Quality = LegendaryQuality }];

        var parser = await AnalyzeAs(combatant, Cast(PullStart + 1_000, Spells.HoldTheLine));

        var holdTheLine = parser.GetModule<HelenaAbilities>().ShouldNotBeNull()
            .GetAbility(Spells.HoldTheLine.FSLID).ShouldNotBeNull();

        holdTheLine.Charges.ShouldBe(1);
        holdTheLine.CooldownReducedByHaste.ShouldBeFalse();
    }

    [Fact]
    public void Taunt_HasTwoCharges()
    {
        Entry(new HelenaAbilities().Spellbook().ToList(), Spells.Taunt).Charges.ShouldBe(2);
    }

    private static SpellbookAbility Entry(List<SpellbookAbility> spellbook, Spell spell) =>
        spellbook.Single(entry => entry.PrimarySpell.FSLID == spell.FSLID);
}
