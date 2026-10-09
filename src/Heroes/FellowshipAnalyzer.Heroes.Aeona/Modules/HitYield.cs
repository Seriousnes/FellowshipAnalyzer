using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Normalizers;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The healing and Oblivion's Embrace shielding one damage hit produced.</summary>
/// <param name="EffectiveHealing">Effective healing across the allies the hit healed.</param>
/// <param name="Overheal">Overheal across those allies.</param>
/// <param name="AlliesHealed">Allies the hit healed.</param>
/// <param name="ShieldApplied">Absorb the hit added to allies' Oblivion's Embrace shields, summed across allies.</param>
/// <param name="AlliesShielded">Allies whose shield the hit grew.</param>
public readonly record struct HitYield(
    long EffectiveHealing,
    long Overheal,
    int AlliesHealed,
    long ShieldApplied,
    int AlliesShielded)
{
    /// <summary>
    /// What <paramref name="hit"/> produced, from the heals and shields <see cref="HitLinkNormalizer"/>
    /// linked to it. A shield's absorb is the application's absorb less the absorb left on the shield it
    /// replaced.
    /// </summary>
    /// <param name="hit">An Oblivion, Erasure, Entropy's Claim, or Entropic Burst damage event.</param>
    public static HitYield Of(DamageEvent hit)
    {
        long healing = 0;
        long overheal = 0;
        var healed = 0;
        foreach (var heal in hit.RelatedEvents<HealEvent>(HitLinkNormalizer.Heals))
        {
            healing += heal.Amount;
            overheal += heal.Overheal ?? 0;
            healed++;
        }

        long shield = 0;
        var shielded = 0;
        foreach (var apply in hit.RelatedEvents<ApplyBuffEvent>(HitLinkNormalizer.Shields))
        {
            var remainder = apply.RelatedEvent<RemoveBuffEvent>(HitLinkNormalizer.Replaced)?.Absorb ?? 0;
            var added = (apply.Absorb ?? 0) - remainder;
            if (added <= 0) continue;

            shield += added;
            shielded++;
        }

        return new HitYield(healing, overheal, healed, shield, shielded);
    }
}
