using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class EffectDataTests
{
    private static readonly MergeResult Merged = MergeEngine.Run(MergeInputs.Load());

    private static ExportEffectData Effect(int id) => Merged.EffectData.Single(effect => effect.Id == id);

    private static AttributeModifier Only(int id) => Effect(id).Modifiers.ShouldHaveSingleItem();

    private static void ShouldBe(AttributeModifier modifier, StatAttribute attribute, ModifierOp op, double? value, double[]? ranks = null, double? coefficient = null)
    {
        modifier.Attribute.ShouldBe(attribute);
        modifier.Op.ShouldBe(op);
        modifier.Value.ShouldBe(value);
        modifier.Ranks.ShouldBe(ranks);
        modifier.Coefficient.ShouldBe(coefficient);
    }

    [Theory]
    [InlineData(2253)]
    [InlineData(1350)]
    [InlineData(2277)]
    [InlineData(2662)]
    public void SpiritOfHeroism_AddsThirtyPercentHasteForTwentySeconds(int id)
    {
        ShouldBe(Only(id), StatAttribute.Haste, ModifierOp.Additive, 0.3);
        Effect(id).Duration.ShouldBe(20);
    }

    [Fact]
    public void ShadowsDefeat_MultipliesHasteByOnePointTwoForSixtySeconds()
    {
        ShouldBe(Only(2221), StatAttribute.Haste, ModifierOp.Multiplicitive, 1.2);
        Effect(2221).Duration.ShouldBe(60);
    }

    [Fact]
    public void RuneRush_MultipliesHasteByOnePointOneFiveForTenSeconds()
    {
        ShouldBe(Only(2958), StatAttribute.Haste, ModifierOp.Multiplicitive, 1.15);
        Effect(2958).Duration.ShouldBe(10);
    }

    [Fact]
    public void BlessingOfTheBear_CompoundsHasteByOnePointOhFiveForTwentyTwoSeconds()
    {
        ShouldBe(Only(3293), StatAttribute.Haste, ModifierOp.MultiplyCompound, 1.05);
        Effect(3293).Duration.ShouldBe(22);
    }

    [Fact]
    public void FocusedHaste_AddsHasteRatingPerStackForEightSecondsUpToFiveStacks()
    {
        ShouldBe(Only(2141), StatAttribute.HasteRating, ModifierOp.Additive, 4, [4, 8, 12, 16]);
        Effect(2141).Duration.ShouldBe(8);
        Effect(2141).MaxStacks.ShouldBe(5);
    }

    [Fact]
    public void InspiredAllegiance_AddsHasteRatingByRankForEightSeconds()
    {
        ShouldBe(Only(2112), StatAttribute.HasteRating, ModifierOp.Additive, 6, [6, 10, 13, 17]);
        Effect(2112).Duration.ShouldBe(8);
    }

    [Fact]
    public void TheWayfarer_AddsHasteByRankForFourteenSeconds()
    {
        ShouldBe(Only(3184), StatAttribute.Haste, ModifierOp.Additive, 0.06, [0.06, 0.1, 0.16, 0.25]);
        Effect(3184).Duration.ShouldBe(14);
    }

    [Fact]
    public void StrandOfEternity_MultipliesCooldownRecoveryByOnePointOneWithNoDuration()
    {
        ShouldBe(Only(3087), StatAttribute.CooldownRecovery, ModifierOp.Multiplicitive, 1.1);
        Effect(3087).Duration.ShouldBeNull();
    }

    [Fact]
    public void AshasArcaneEmbrace_MultipliesCooldownRecoveryByEight() =>
        ShouldBe(Only(2279), StatAttribute.CooldownRecovery, ModifierOp.Multiplicitive, 8);

    [Fact]
    public void GloriousPurpose_MultipliesCooldownRecoveryByThreeForSixSeconds()
    {
        ShouldBe(Only(80), StatAttribute.CooldownRecovery, ModifierOp.Multiplicitive, 3);
        Effect(80).Duration.ShouldBe(6);
    }

    [Fact]
    public void FleetingHour_MultipliesCooldownRecoveryByOnePointFiveForFifteenSeconds()
    {
        ShouldBe(Only(2744), StatAttribute.CooldownRecovery, ModifierOp.Multiplicitive, 1.5);
        Effect(2744).Duration.ShouldBe(15);
    }

    [Theory]
    [InlineData(2820, 3)]
    [InlineData(3266, 2)]
    public void ConvergingTimelines_MultipliesCooldownRecoveryForHalfASecond(int id, double value)
    {
        ShouldBe(Only(id), StatAttribute.CooldownRecovery, ModifierOp.Multiplicitive, value);
        Effect(id).Duration.ShouldBe(0.5);
    }

    [Fact]
    public void EventHorizon_CompoundsCooldownRecoveryWithNoValueForTwentySeconds()
    {
        ShouldBe(Only(2314), StatAttribute.CooldownRecovery, ModifierOp.MultiplyCompound, null);
        Effect(2314).Duration.ShouldBe(20);
    }

    [Fact]
    public void XavianEffect2719_AddsCooldownRecoveryFinalByCoefficient() =>
        ShouldBe(Only(2719), StatAttribute.CooldownRecovery, ModifierOp.AddFinal, null, null, 0.33334);

    [Theory]
    [InlineData(1963, 0.04)]
    [InlineData(1964, 0.12)]
    public void BlessingOfTheCommander_AddsCooldownReduction(int id, double value) =>
        ShouldBe(Only(id), StatAttribute.CooldownReduction, ModifierOp.Additive, value);

    [Fact]
    public void Bloodbath_AcceleratesAbility2295ByTwoForThreeSeconds()
    {
        Effect(3216).Duration.ShouldBe(3);
        Effect(3216).CooldownAccelerations.ShouldBe([new CooldownAccelerationChange(2295, 2)]);
    }

    [Fact]
    public void SkylitGrace_AcceleratesAbility1304ByOnePointTwo() =>
        Effect(2316).CooldownAccelerations.ShouldBe([new CooldownAccelerationChange(1304, 1.2)]);

    [Fact]
    public void KillEmAll_AcceleratesAbility1929ByOnePointSixForEightSeconds()
    {
        Effect(3160).Duration.ShouldBe(8);
        Effect(3160).CooldownAccelerations.ShouldBe([new CooldownAccelerationChange(1929, 1.6)]);
    }

    [Fact]
    public void FreezingTorrent_AcceleratesAbility1031ByOne() =>
        Effect(2315).CooldownAccelerations.ShouldBe([new CooldownAccelerationChange(1031, 1)]);

    [Fact]
    public void EveryEffectInTheSection_HasAModifierOrAnAcceleration() =>
        Merged.EffectData.ShouldAllBe(effect => effect.Modifiers.Length > 0 || effect.CooldownAccelerations.Length > 0);

    [Fact]
    public void TheSection_IsInAscendingIdOrderWithNoRepeat() =>
        Merged.EffectData.Select(effect => effect.Id).ShouldBe(Merged.EffectData.Select(effect => effect.Id).Distinct().Order());

    [Fact]
    public void RatingCurves_HaveTheFourSecondaryStats()
    {
        Merged.RatingCurves.Select(curve => curve.Stat).ShouldBe(["CritChance", "Haste", "Expertise", "Spirit"]);

        var haste = Merged.RatingCurves.Single(curve => curve.Stat == "Haste").Curve;
        haste.BaseStatMultiplier.ShouldBe(0.16);
        haste.Brackets.ShouldBe([(10, 1), (15, 0.98), (20, 0.96), (25, 0.94)]);
        haste.FinalPenaltyPercentage.ShouldBe(0.92);

        foreach (var curve in Merged.RatingCurves)
        {
            curve.Curve.BaseStatMultiplier.ShouldBe(haste.BaseStatMultiplier);
            curve.Curve.Brackets.ShouldBe(haste.Brackets);
            curve.Curve.FinalPenaltyPercentage.ShouldBe(haste.FinalPenaltyPercentage);
        }
    }

    [Fact]
    public void TheSections_SurviveASerializeAndDeserializeRoundTrip()
    {
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(Merged));

        restored.EffectData.Select(effect => effect.Id).ShouldBe(Merged.EffectData.Select(effect => effect.Id));
        restored.RatingCurves.Select(curve => curve.Stat).ShouldBe(Merged.RatingCurves.Select(curve => curve.Stat));
        var wayfarer = restored.EffectData.Single(effect => effect.Id == 3184);
        wayfarer.Modifiers.Single().Ranks.ShouldBe([0.06, 0.1, 0.16, 0.25]);
        wayfarer.Duration.ShouldBe(14);
    }
}
