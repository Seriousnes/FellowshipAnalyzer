using FellowshipAnalyzer.Core.Game;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// <see cref="EffectData"/> and <see cref="RatingCurves"/> are generated from <c>data/spelldb.json</c>, so an
/// effect's modifiers, duration and Cooldown Acceleration resolve from its native id alone.
/// </summary>
public sealed class EffectDataTests
{
    [Fact]
    public void ModifiersOf_GivesSpiritOfHeroismItsHasteModifier()
    {
        var modifier = Assert.Single(EffectData.ModifiersOf(2253).ToArray());

        Assert.Equal(StatAttribute.Haste, modifier.Attribute);
        Assert.Equal(ModifierOp.Additive, modifier.Op);
        Assert.Equal(0.3, modifier.Value);
    }

    [Fact]
    public void DurationOf_GivesGloriousPurposeSixSeconds() =>
        Assert.Equal(6, EffectData.DurationOf(80));

    [Fact]
    public void MaxStacksOf_GivesFocusedHasteFiveStacks() =>
        Assert.Equal(5, EffectData.MaxStacksOf(2141));

    [Fact]
    public void CooldownAccelerationsOf_GivesBloodbathItsAbility()
    {
        var change = Assert.Single(EffectData.CooldownAccelerationsOf(3216).ToArray());

        Assert.Equal(2295, change.AbilityId);
        Assert.Equal(2, change.Value);
    }

    [Fact]
    public void ALookupOutsideTheSection_IsEmptyOrNull()
    {
        Assert.True(EffectData.ModifiersOf(1).IsEmpty);
        Assert.True(EffectData.CooldownAccelerationsOf(1).IsEmpty);
        Assert.Null(EffectData.DurationOf(1));
        Assert.Null(EffectData.MaxStacksOf(1));
    }

    [Fact]
    public void EffectIds_AreAscendingAndEachHasModifiersOrAcceleration()
    {
        var ids = EffectData.EffectIds.ToArray();

        Assert.Equal(ids.Order(), ids);
        Assert.All(ids, id => Assert.False(EffectData.ModifiersOf(id).IsEmpty && EffectData.CooldownAccelerationsOf(id).IsEmpty));
    }

    [Fact]
    public void Haste_HasTheExportsRatingCurve()
    {
        Assert.Equal(0.16, RatingCurves.Haste.BaseStatMultiplier);
        Assert.Equal([(10, 1), (15, 0.98), (20, 0.96), (25, 0.94)], RatingCurves.Haste.Brackets);
        Assert.Equal(0.92, RatingCurves.Haste.FinalPenaltyPercentage);
    }
}
