using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Reads the modifiers of an effect from the generated <see cref="EffectData"/>. Hero projects call it for the
/// value of an effect's attribute modifier and for the Cooldown Recovery modifier they join to
/// <see cref="StatTracker"/>'s Cooldown Recovery pool.
/// </summary>
public static class EffectModifierValue
{
    /// <summary>
    /// The value of the <paramref name="attribute"/> modifier of the effect at <paramref name="effectId"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives the effect no such modifier.</exception>
    public static double Of(int effectId, StatAttribute attribute) => ModifierOf(effectId, attribute).Value;

    /// <summary>
    /// The <see cref="StatAttribute.CooldownRecovery"/> modifier of the effect at <paramref name="effectId"/>,
    /// with the value and the <see cref="ModifierOp"/> the export gives it. Pass it to
    /// <see cref="StatTracker.AddCooldownModifier"/> with <see cref="CooldownPool.CooldownRecovery"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives the effect no Cooldown Recovery modifier.</exception>
    public static CooldownModifier CooldownRecoveryOf(int effectId)
    {
        var modifier = ModifierOf(effectId, StatAttribute.CooldownRecovery);
        return new CooldownModifier(modifier.Value) { Op = modifier.Op };
    }

    private static (double Value, ModifierOp Op) ModifierOf(int effectId, StatAttribute attribute)
    {
        foreach (var modifier in EffectData.ModifiersOf(effectId))
        {
            if (modifier.Attribute == attribute && modifier.Value is { } value) return (value, modifier.Op);
        }

        throw new InvalidOperationException($"The export gives effect {effectId} no {attribute} modifier.");
    }
}
