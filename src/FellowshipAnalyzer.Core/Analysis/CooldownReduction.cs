namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's total Ability Cooldown Reduction (ACR): <c>effective = base × (1 - acr)</c>.
/// </summary>
/// <remarks>
/// ACR is a distinct mechanic from the cooldown recovery pool <see cref="SpellUsable.EffectiveRate"/>
/// models, and composes with it rather than feeding it: recovery divides, ACR multiplies. It applies to
/// base cooldowns only (at 100% an ability has no cooldown whatever its base duration); flat cooldown
/// reductions such as Rolling Flames are deliberately left at their full value.
///
/// Sources combine additively. Gem power is the only one wired today; register others with
/// <see cref="Add"/>. Every current source is fixed for a fight, so <see cref="SpellUsable"/> reads
/// <see cref="Current"/> when a cooldown starts and never rescales one in flight. A source that can
/// change mid-fight would need that rescale, the way haste changes do.
/// </remarks>
public sealed partial class CooldownReduction(Lazy<GemPowers> gemPowers) : Analyzer
{
    private double _additional;

    /// <summary>
    /// Total Ability Cooldown Reduction as a fraction (0.12 = 12%), capped at 1.0 so a cooldown can be
    /// erased but never driven below zero.
    /// </summary>
    public double Current => Math.Min(1.0, _gemPowers.AbilityCooldownReduction + _additional);

    /// <summary>
    /// Registers Ability Cooldown Reduction from a source other than gem power, as a fraction
    /// (0.10 = 10%). Adds to the pool rather than multiplying with it.
    /// </summary>
    public void Add(double acr) => _additional += acr;

    /// <summary>
    /// Scales a base cooldown duration by the current ACR: at 10% ACR a 30s cooldown becomes 27s. Flat
    /// cooldown reductions are not passed through here; they apply in full.
    /// </summary>
    public int Scale(int milliseconds) => (int)(milliseconds * (1.0 - Current));
}
