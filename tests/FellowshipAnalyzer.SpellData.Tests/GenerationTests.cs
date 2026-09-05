using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;

using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class GenerationTests
{
    private static ResourceGeneration Stated(string description) =>
        Generation.Read(description).Stated.ShouldNotBeNull();

    [Fact]
    public void PlainStatement_IsAPerCastFlatAmount()
    {
        var stated = Stated("<rt.mana>Generates 6 Chrona.</>");
        stated.Resource.ShouldBe(ResourceTypes.Primary);
        stated.Amount.ShouldBe(6);
        stated.Measure.ShouldBe(GenerationMeasure.Flat);
        stated.Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void CriticalSentence_BindsToTheAmountInTheSameDescription()
    {
        var stated = Stated("<rt.mana>Generates 20 Chrona.</>\r\n<rt.mana>Critical Strike chance to generate 30 Chrona.</>");
        stated.Amount.ShouldBe(20);
        stated.CriticalAmount.ShouldBe(30);
    }

    [Fact]
    public void AbsentCriticalSentence_LeavesNoCriticalAmount() =>
        Stated("<rt.mana>Generates 5 Chrona.</>").CriticalAmount.ShouldBeNull();

    [Fact]
    public void PerBoltQualifier_IsAPerHitAmount()
    {
        var stated = Stated("<rt.mana>Generates 4 Chrona per bolt.</>");
        stated.Amount.ShouldBe(4);
        stated.Trigger.ShouldBe(GenerationTrigger.PerHit);
    }

    [Fact]
    public void EachTimeItDealsDamage_IsAPerHitAmount()
    {
        var stated = Stated(
            "<rt.mana>Each time</> <rt.absorb>Entrropy's Claim</> <rt.mana>deals damage, it generates 4 Chrona.</>\r\n"
            + "<rt.mana>Critical Strike chance to generate 6 Chrona.</>");
        stated.Trigger.ShouldBe(GenerationTrigger.PerHit);
        stated.Amount.ShouldBe(4);
        stated.CriticalAmount.ShouldBe(6);
    }

    [Fact]
    public void OverItsFullDuration_IsSpreadOverTheDuration()
    {
        var stated = Stated("<rt.mana>Generates 30 Cinders over its full duration.</>");
        stated.Trigger.ShouldBe(GenerationTrigger.OverDuration);
        stated.Amount.ShouldBe(30);
    }

    [Fact]
    public void InstantRestore_IsAShareOfTheMaximumPool()
    {
        var stated = Stated("Instantly restore <rt.mana>30% of your maximum mana</>.");
        stated.Resource.ShouldBe(ResourceTypes.Mana);
        stated.Amount.ShouldBe(0.3);
        stated.Measure.ShouldBe(GenerationMeasure.FractionOfMaximum);
        stated.Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void PerStackAtExpiry_IsAShareOfTheMaximumPoolPerStack()
    {
        var stated = Stated(
            "Each time you use <rt.mana>Chrona</> you gain 1 stack of <rt.effect>Chrona Tap</> for 9 seconds, up to 10 stacks.\r\n\r\n"
            + "<rt.effect>Chrona Tap</> replenishes <rt.mana>1.3% of your Maximum Mana per stack</> when it expires.");
        stated.Resource.ShouldBe(ResourceTypes.Mana);
        stated.Amount.ShouldBe(0.013);
        stated.Measure.ShouldBe(GenerationMeasure.FractionOfMaximum);
        stated.Trigger.ShouldBe(GenerationTrigger.PerStack);
    }

    [Fact]
    public void WhenYouCast_IsAFlatAmountOnTheCast()
    {
        var stated = Stated("When you cast <rt.absorb>Fleeting Hour</>, you gain <rt.mana>30 Chrona</>.");
        stated.Amount.ShouldBe(30);
        stated.Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void IncreaseClause_CarriesTheFractionAndNoTrigger()
    {
        var stated = Stated(
            "When you are <rt.mana>above 50% Chrona</> you <rt.warning>deal 15% more damage</> with your abilities that do not spend <rt.mana>Chrona</>.\r\n\r\n"
            + "When you are <rt.mana>below 50% Chrona</> you <rt.mana>generate 25% more Chrona</>.");
        stated.Resource.ShouldBe(ResourceTypes.Primary);
        stated.Amount.ShouldBe(0.25);
        stated.Measure.ShouldBe(GenerationMeasure.Increase);
        stated.Trigger.ShouldBeNull();
    }

    [Fact]
    public void IncreaseClauseInsideALongerSentence_IsStillRead()
    {
        var stated = Stated(
            "<rt.absorb>Time Shard's</> cast time is doubled and <rt.warning>deals 900% more damage</>, "
            + "<rt.mana>generates 900% increased Chrona</>, and <rt.heal>heals all allies</>.");
        stated.Amount.ShouldBe(9);
        stated.Measure.ShouldBe(GenerationMeasure.Increase);
    }

    [Fact]
    public void ADamageIncreaseSharingTheSentence_IsNotTheOneRead()
    {
        var stated = Stated(
            "<rt.absorb>Time Shard's</> cast time is doubled and <rt.warning>deals 500% more damage</>, "
            + "<rt.mana>generates 900% increased Chrona</>, and <rt.heal>heals all allies</>.");
        stated.Amount.ShouldBe(9);
        stated.Resource.ShouldBe(ResourceTypes.Primary);
    }

    [Fact]
    public void ADamageIncreaseAlone_ReadsAsNothing()
    {
        var reading = Generation.Read(
            "<rt.absorb>Time Shard's</> cast time is doubled and <rt.warning>deals 900% more damage</>.");
        reading.Stated.ShouldBeNull();
        reading.Unclaimed.ShouldBeEmpty();
    }

    [Fact]
    public void AThresholdBesideAResource_IsNotReadAsAnAmount()
    {
        var reading = Generation.Read(
            "When you are <rt.mana>above 50% Chrona</> you <rt.warning>deal 15% more damage</> with your abilities.");
        reading.Stated.ShouldBeNull();
        reading.Unclaimed.ShouldBeEmpty();
    }

    [Fact]
    public void ADescriptionNamingNoResource_ReadsAsNothing()
    {
        var reading = Generation.Read("Deals <rt.warning>{BackstabDmg}</> damage to target enemy.");
        reading.Stated.ShouldBeNull();
        reading.Unclaimed.ShouldBeEmpty();
    }

    [Fact]
    public void AnAmountInAFormNoRuleClaims_IsReportedRatherThanGuessedAt()
    {
        var reading = Generation.Read("<rt.mana>Generates 6 Fury plus an amount based on the total damage it dealt</>");
        reading.Stated.ShouldBeNull();
        reading.Unclaimed.ShouldHaveSingleItem();
    }
}
