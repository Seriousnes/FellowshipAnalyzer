using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;
using VigourAbilities = FellowshipAnalyzer.Heroes.Vigour.Modules.Abilities;
using VigourTalents = FellowshipAnalyzer.Core.Common.Spells.VigourTalents;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class VigourSpellbookTests
{
    private const int HasteRating = 1000;

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
        abilities.GetAbility(Spells.LuminousBarrier.FSLID).ShouldNotBeNull().CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
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
