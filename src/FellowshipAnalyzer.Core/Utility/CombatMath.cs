using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Utility;

/// <summary>
/// Static helpers for attributing damage and healing to combat effects.
/// Ported from WoWAnalyzer's EventCalculateLib.
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// Calculates the effective damage attributable to a percent damage buff.
    /// The bonus damage is treated as marginal — it is the first to be consumed by absorbs.
    ///
    /// For example, an effect that boosts damage by 20%: pass <paramref name="increase"/> = 0.20.
    /// A raw hit of 1200 would yield 1200 - (1200 / 1.20) = 200 attributable damage.
    /// </summary>
    /// <param name="e">The damage event boosted by the effect.</param>
    /// <param name="increase">The boost's added multiplier (for +20% pass 0.20).</param>
    /// <returns>The amount of damage on this event attributable to the given boost.</returns>
    public static long CalculateEffectiveDamage(DamageEvent e, double increase)
    {
        var raw = e.Amount + (e.Absorbed ?? 0);
        return raw - (long)(raw / (1.0 + increase));
    }

    /// <summary>
    /// Calculates the damage that was mitigated by a flat percent damage reduction.
    ///
    /// For example, a 20% reduction: pass <paramref name="reduction"/> = 0.20.
    /// An event showing 800 damage (after reduction) would yield (800 / 0.80) * 0.20 = 200 mitigated.
    /// </summary>
    /// <param name="e">The damage event affected by the reduction.</param>
    /// <param name="reduction">The reduction factor in [0, 1) (for 20% pass 0.20).</param>
    /// <returns>The amount of damage prevented by the reduction.</returns>
    public static long CalculateEffectiveDamageReduction(DamageEvent e, double reduction)
    {
        var raw = e.Amount + (e.Absorbed ?? 0);
        return (long)(raw / (1.0 - reduction) * reduction);
    }
}
