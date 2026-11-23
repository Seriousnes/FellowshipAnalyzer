using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Game;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// <see cref="EffectModifierValue"/> reads an effect's modifiers from <see cref="EffectData"/>, keeping the
/// operation the export gives a Cooldown Recovery modifier.
/// </summary>
public sealed class EffectModifierValueTests
{
    private const int SpiritOfHeroism = 2253;
    private const int ConvergingTimelines = 3266;
    private const int ConvergingTimelinesLonesomeSong = 2820;

    [Fact]
    public void Of_GivesSpiritOfHeroismItsHasteValue() =>
        Assert.Equal(0.3, EffectModifierValue.Of(SpiritOfHeroism, StatAttribute.Haste));

    [Fact]
    public void Of_ThrowsWhenTheEffectHasNoSuchModifier() =>
        Assert.Throws<InvalidOperationException>(() => EffectModifierValue.Of(SpiritOfHeroism, StatAttribute.CooldownRecovery));

    [Fact]
    public void CooldownRecoveryOf_KeepsTheExportsValueAndOperation()
    {
        var export = Assert.Single(
            EffectData.ModifiersOf(ConvergingTimelines).ToArray(),
            modifier => modifier.Attribute == StatAttribute.CooldownRecovery);

        var modifier = EffectModifierValue.CooldownRecoveryOf(ConvergingTimelines);

        Assert.Equal(export.Value, modifier.Value);
        Assert.Equal(export.Op, modifier.Op);
    }

    [Fact]
    public void CooldownRecoveryOf_GivesEachEffectItsOwnModifier() =>
        Assert.NotEqual(
            EffectModifierValue.CooldownRecoveryOf(ConvergingTimelines),
            EffectModifierValue.CooldownRecoveryOf(ConvergingTimelinesLonesomeSong));

    [Fact]
    public void CooldownRecoveryOf_ThrowsWhenTheEffectHasNoCooldownRecoveryModifier() =>
        Assert.Throws<InvalidOperationException>(() => EffectModifierValue.CooldownRecoveryOf(SpiritOfHeroism));
}
