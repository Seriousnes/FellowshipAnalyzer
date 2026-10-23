using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Heroes.Sylvie.Analysis;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;
using SylvieAbilities = FellowshipAnalyzer.Heroes.Sylvie.Modules.Abilities;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

/// <summary>
/// Heart Bloom has 2 charges and TimeRate Haste with Flower Power, and Ironleaf Ward has both with the
/// Unrelenting Thicket legendary. Each keeps 1 charge without its own source.
/// </summary>
public sealed class SylvieChargeGrantTests
{
    [Fact]
    public void HeartBloomsGrantNamesFlowerPower()
    {
        var grant = Spells.HeartBloom.ChargeGrant.ShouldNotBeNull();

        grant.Source.ShouldBe(GrantSource.Talent);
        grant.Id.ShouldBe(SylvieTalents.FlowerPower);
        grant.Charges.ShouldBe(2);
        grant.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public void IronleafWardsGrantNamesUnrelentingThicket()
    {
        var grant = Spells.IronleafWard.ChargeGrant.ShouldNotBeNull();

        grant.Source.ShouldBe(GrantSource.LegendaryItem);
        grant.Id.ShouldBe(SylvieLegendaries.UnrelentingThicket);
        grant.Charges.ShouldBe(2);
        grant.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public async Task FlowerPower_GivesHeartBloomTwoChargesAndTimeRateHaste()
    {
        var abilities = AbilitiesOf(await Analyze(Combatant(SylvieTalents.FlowerPower)));

        abilities.GetMaxCharges(Spells.HeartBloom.FSLID).ShouldBe(2);
        abilities.GetAbility(Spells.HeartBloom.FSLID)!.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
        abilities.GetMaxCharges(Spells.IronleafWard.FSLID).ShouldBe(1);
    }

    [Fact]
    public async Task UnrelentingThicket_GivesIronleafWardTwoChargesAndTimeRateHaste()
    {
        var abilities = AbilitiesOf(await Analyze(CombatantWithLegendary(SylvieLegendaries.UnrelentingThicket)));

        abilities.GetMaxCharges(Spells.IronleafWard.FSLID).ShouldBe(2);
        abilities.GetAbility(Spells.IronleafWard.FSLID)!.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
        abilities.GetMaxCharges(Spells.HeartBloom.FSLID).ShouldBe(1);
    }

    [Fact]
    public async Task WithoutEitherSource_BothKeepOneCharge()
    {
        var abilities = AbilitiesOf(await Analyze(Combatant()));

        abilities.GetMaxCharges(Spells.HeartBloom.FSLID).ShouldBe(1);
        abilities.GetMaxCharges(Spells.IronleafWard.FSLID).ShouldBe(1);
    }

    private static SylvieAbilities AbilitiesOf(SylvieCombatLogParser parser) =>
        parser.GetModule<SylvieAbilities>().ShouldNotBeNull();
}
