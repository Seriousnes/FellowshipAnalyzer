using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;

using AeonaSpells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;
using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.Aeona.Talents;
using Items = FellowshipAnalyzer.Core.Common.Items.Items;

using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class ResourceGenerationMergeTests
{
    private static MergeResult Run() => MergeEngine.Run(MergeInputs.Load());

    private static ResourceGeneration Merged(string scope, string member) =>
        Run().Spells.Single(s => s.Scope == scope && s.Member == member).Spell.ResourceGeneration.ShouldNotBeNull();

    private static ResourceGeneration MergedTalent(string scope, string member) =>
        Run().Talents.Single(t => t.Scope == scope && t.Member == member).Spell.ResourceGeneration.ShouldNotBeNull();

    [Fact]
    public void AbilityStatingAPerCastAmount_HasItWithItsCriticalAmount()
    {
        var generation = Merged("aeona", "EchoesOfRuin");
        generation.Resource.ShouldBe(ResourceTypes.Primary);
        generation.Amount.ShouldBe(6);
        generation.CriticalAmount.ShouldBe(9);
        generation.Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void AbilityStatingAPerDamageAmount_IsKeptApartFromAPerCastOne()
    {
        Merged("aeona", "EntropyClaim").Trigger.ShouldBe(GenerationTrigger.PerHit);
        Merged("aeona", "TemporalBarrage").Trigger.ShouldBe(GenerationTrigger.PerHit);
        Merged("aeona", "TimeShard").Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void AbilityStatingNoCriticalAmount_HasNone() =>
        Merged("aeona", "FlashRevision").CriticalAmount.ShouldBeNull();

    [Fact]
    public void RelicStatingAShareOfTheMaximumPool_IsRoutedToItemsWithThatShare()
    {
        var generation = Merged("items", "RestoreMana");
        generation.Resource.ShouldBe(ResourceTypes.Mana);
        generation.Amount.ShouldBe(0.3);
        generation.Measure.ShouldBe(GenerationMeasure.FractionOfMaximum);
    }

    [Fact]
    public void TalentStatingAnAmount_HasItOnTheTalentTheExportRoutesToTheHero()
    {
        var generation = MergedTalent("aeona", "SurgingChrona");
        generation.Resource.ShouldBe(ResourceTypes.Primary);
        generation.Amount.ShouldBe(30);
        generation.Trigger.ShouldBe(GenerationTrigger.PerCast);
    }

    [Fact]
    public void TalentStatingAnIncrease_HasTheFractionAndNoTrigger()
    {
        MergedTalent("aeona", "Synchronicity").Amount.ShouldBe(0.25);
        MergedTalent("aeona", "Synchronicity").Measure.ShouldBe(GenerationMeasure.Increase);
        MergedTalent("aeona", "ContinuumShift").Amount.ShouldBe(9);
        MergedTalent("aeona", "ContinuumShift").Trigger.ShouldBeNull();
    }

    [Fact]
    public void ATalentAndAnEffectSharingAName_StayApartInTheirOwnSections()
    {
        var result = Run();
        result.Spells.Single(s => s.Scope == "aeona" && s.Member == "ContinuumShift").Kind.ShouldBe(SpellKind.Effect);
        result.Talents.Single(t => t.Scope == "aeona" && t.Member == "ContinuumShift").Kind.ShouldBe(SpellKind.Talent);
    }

    [Fact]
    public void TalentStatingNoAmount_HasNoGeneration() =>
        Run().Talents.Single(t => t.Scope == "aeona" && t.Member == "Uchronia")
            .Spell.ResourceGeneration.ShouldBeNull();

    [Fact]
    public void AeonaKit_LeavesNoGenerationSentenceUnclaimed() =>
        Run().Gaps.ShouldNotContain(g => g.Scope == "aeona" && g.Kind == GapKind.UnclaimedGeneration);

    [Fact]
    public void GeneratedRegistry_ReadsTheAmountOffTheSpell()
    {
        AeonaSpells.EntropyClaim.ResourceGeneration!.Amount.ShouldBe(4);
        AeonaSpells.EntropyClaim.ResourceGeneration!.CriticalAmount.ShouldBe(6);
        AeonaSpells.EntropyClaim.ResourceGeneration!.Trigger.ShouldBe(GenerationTrigger.PerHit);
        AeonaSpells.EntropyClaim.ResourceGeneration!.Resource.ShouldBe(ResourceTypes.Primary);
    }

    [Fact]
    public void GeneratedRegistry_ReadsATalentIncreaseOffTheTalent()
    {
        AeonaTalents.Synchronicity.ResourceGeneration!.Amount.ShouldBe(0.25);
        AeonaTalents.Synchronicity.ResourceGeneration!.Measure.ShouldBe(GenerationMeasure.Increase);
        AeonaTalents.ContinuumShift.ResourceGeneration!.Amount.ShouldBe(9);
    }

    [Fact]
    public void GeneratedRegistry_ReadsAShareOfTheMaximumPoolOffTheRelic()
    {
        Items.RestoreMana.ResourceGeneration!.Amount.ShouldBe(0.3);
        Items.RestoreMana.ResourceGeneration!.Measure.ShouldBe(GenerationMeasure.FractionOfMaximum);
        Items.RestoreMana.ResourceGeneration!.Resource.ShouldBe(ResourceTypes.Mana);
    }

    [Fact]
    public void GeneratedRegistry_ReadsTheChronaTapAmountOffTheTalent()
    {
        AeonaTalents.ChronaTap.ResourceGeneration!.Amount.ShouldBe(0.013);
        AeonaTalents.ChronaTap.ResourceGeneration!.Measure.ShouldBe(GenerationMeasure.FractionOfMaximum);
        AeonaTalents.ChronaTap.ResourceGeneration!.Trigger.ShouldBe(GenerationTrigger.PerStack);
    }
}
