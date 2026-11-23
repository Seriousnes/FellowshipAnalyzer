using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;
using VigourAbilities = FellowshipAnalyzer.Heroes.Vigour.Modules.Abilities;
using VigourTalents = FellowshipAnalyzer.Core.Common.Spells.VigourTalents;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class VigourSpellbookTests
{
    private const int HasteRating = 1000;
    private const int EmeraldRankTen = 1_500;

    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new VigourAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public async Task SacredBarrierGivesLuminousBarrierTwoChargesOnItsTooltipCooldown()
    {
        var parser = await Analyze(Combatant(VigourTalents.SacredBarrier), Cast(PullStart, Spells.Dawnflare));

        var abilities = parser.GetModule<VigourAbilities>().ShouldNotBeNull();

        abilities.GetMaxCharges(Spells.LuminousBarrier.FSLID).ShouldBe(2);
        abilities.GetExpectedCooldown(Spells.LuminousBarrier.FSLID).ShouldBe(Spells.LuminousBarrier.Cooldown.ShouldNotBeNull());
        abilities.GetAbility(Spells.LuminousBarrier.FSLID).ShouldNotBeNull().PrimarySpell.CooldownScaling
            .ShouldBe(new CooldownScaling { TimeRateCooldownRecovery = true, TimeRateHaste = true });
    }

    [Fact]
    public void NoEntry_DeclaresAGlobalCooldown()
    {
        foreach (var entry in new VigourAbilities().Spellbook())
            entry.Gcd.ShouldBeNull();
    }

    [Fact]
    public void RemoveMagicAndRepositoryOfFrozenLightAreOnTheGlobalCooldownAndLevitateIsNot()
    {
        var spellbook = new VigourAbilities().Spellbook().ToList();

        spellbook.Single(entry => entry.PrimarySpell.FSLID == Spells.RemoveMagic.FSLID).OnGlobalCooldown.ShouldBeTrue();
        spellbook.Single(entry => entry.PrimarySpell.FSLID == CoreItems.RepositoryOfFrozenLight.FSLID).OnGlobalCooldown.ShouldBeTrue();
        spellbook.Single(entry => entry.PrimarySpell.FSLID == Spells.Levitate.FSLID).OnGlobalCooldown.ShouldBeFalse();
        CoreItems.RepositoryOfFrozenLight.GlobalCooldown.ShouldBe(1.5);
        CoreItems.RepositoryOfFrozenLight.GlobalCooldownScalesWithHaste.ShouldBe(true);
    }

    [Fact]
    public void RadiantBlastAndCircleOfLightScaleTheirCooldownByCooldownReductionHasteAndCooldownRecovery()
    {
        var scaling = new CooldownScaling { DurationCooldownReduction = true, DurationHaste = true, TimeRateCooldownRecovery = true };

        Spells.RadiantBlast.Cooldown.ShouldBe(10);
        Spells.RadiantBlast.CooldownScaling.ShouldBe(scaling);
        Spells.CircleOfLight.Cooldown.ShouldBe(15);
        Spells.CircleOfLight.CooldownScaling.ShouldBe(scaling);
    }

    [Fact]
    public void LevitateTakesOnlyCooldownRecoveryAndRemoveMagicHasNoScaling()
    {
        Spells.Levitate.Charges.ShouldBe(2);
        Spells.Levitate.CooldownScaling.ShouldBe(new CooldownScaling { TimeRateCooldownRecovery = true });
        Spells.RemoveMagic.Cooldown.ShouldBe(6);
        Spells.RemoveMagic.CooldownScaling.ShouldBeNull();
    }

    [Fact]
    public async Task EmeraldRankTenShortensRadiantBlastAndLeavesRemoveMagicAtItsBaseCooldown()
    {
        var combatant = Combatant();
        combatant.Emerald = EmeraldRankTen;

        var parser = await Analyze(
            combatant,
            Cast(PullStart + 1_000, Spells.RadiantBlast),
            Cast(PullStart + 1_000, Spells.RemoveMagic));

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();

        spellUsable.RechargeDuration(Spells.RadiantBlast.FSLID).ShouldBe(8_800);
        spellUsable.RechargeDuration(Spells.RemoveMagic.FSLID).ShouldBe(6_000);
    }

    [Fact]
    public async Task WithoutSacredBarrierLuminousBarrierHasOneCharge()
    {
        var parser = await Analyze(Cast(PullStart, Spells.Dawnflare));

        var abilities = parser.GetModule<VigourAbilities>().ShouldNotBeNull();

        abilities.GetMaxCharges(Spells.LuminousBarrier.FSLID).ShouldBe(1);
        abilities.GetExpectedCooldown(Spells.LuminousBarrier.FSLID).ShouldBe(Spells.LuminousBarrier.Cooldown.ShouldNotBeNull());
        abilities.GetAbility(Spells.LuminousBarrier.FSLID).ShouldNotBeNull().CooldownHaste.ShouldBe(CooldownHasteScaling.Duration);
    }

    [Fact]
    public async Task SacredBarrierAcceleratesLuminousBarrierByHasteOnce()
    {
        var combatant = Combatant(VigourTalents.SacredBarrier);
        combatant.Haste = HasteRating;

        var parser = await Analyze(combatant, Cast(PullStart, Spells.Dawnflare));

        var haste = parser.GetModule<Haste>().ShouldNotBeNull().Current;
        haste.ShouldBeGreaterThan(0);

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();
        spellUsable.EffectiveRate(Spells.LuminousBarrier.FSLID).ShouldBe(1.0 + haste, tolerance: 1e-9);
        ((double)spellUsable.RechargeDurationDuring(Spells.LuminousBarrier.FSLID, PullStart, PullEnd))
            .ShouldBe(Spells.LuminousBarrier.Cooldown.ShouldNotBeNull() * 1000 / (1.0 + haste), tolerance: 1);
    }
}
