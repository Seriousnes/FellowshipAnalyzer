using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Reads the value of one attribute modifier of an effect from the generated <see cref="EffectData"/>.</summary>
internal static class EffectModifierValue
{
    /// <summary>
    /// The value of the <paramref name="attribute"/> modifier of the effect at <paramref name="effectId"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives the effect no such modifier.</exception>
    public static double Of(int effectId, StatAttribute attribute)
    {
        foreach (var modifier in EffectData.ModifiersOf(effectId))
        {
            if (modifier.Attribute == attribute && modifier.Value is { } value) return value;
        }

        throw new InvalidOperationException($"The export gives effect {effectId} no {attribute} modifier.");
    }
}
