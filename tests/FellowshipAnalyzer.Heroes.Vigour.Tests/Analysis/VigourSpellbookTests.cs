using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;
using VigourAbilities = FellowshipAnalyzer.Heroes.Vigour.Modules.Abilities;
using VigourTalents = FellowshipAnalyzer.Core.Common.Spells.VigourTalents;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class VigourSpellbookTests
{
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
        parser.SacredBarrier.ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutSacredBarrierLuminousBarrierHasOneCharge()
    {
        var parser = await Analyze(Cast(PullStart, Spells.Dawnflare));

        var abilities = parser.GetModule<VigourAbilities>().ShouldNotBeNull();

        abilities.GetMaxCharges(Spells.LuminousBarrier.FSLID).ShouldBe(1);
        abilities.GetExpectedCooldown(Spells.LuminousBarrier.FSLID).ShouldBe(Spells.LuminousBarrier.Cooldown.ShouldNotBeNull());
    }
}
