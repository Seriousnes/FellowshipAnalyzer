using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>Reads Cooldown Recovery modifiers of Aeona's effects from the generated <see cref="EffectData"/>.</summary>
internal static class CooldownRecoveryModifiers
{
    /// <summary>
    /// The Cooldown Recovery modifier of <paramref name="effect"/>, with the value and operation the export gives it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export gives <paramref name="effect"/> no Cooldown Recovery modifier.</exception>
    public static CooldownModifier Of(Spell effect)
    {
        foreach (var modifier in EffectData.ModifiersOf(effect.Id))
        {
            if (modifier.Attribute == StatAttribute.CooldownRecovery && modifier.Value is { } value)
                return new CooldownModifier(value) { Op = modifier.Op };
        }

        throw new InvalidOperationException($"The export gives effect {effect.Id} no Cooldown Recovery modifier.");
    }
}
