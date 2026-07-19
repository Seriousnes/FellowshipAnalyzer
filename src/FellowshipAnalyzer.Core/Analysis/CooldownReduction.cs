namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's total Ability Cooldown Reduction (ACR): <c>effective = base * (1 - acr)</c>.
/// </summary>
/// <remarks>
/// ACR is a distinct mechanic from the cooldown recovery pool <see cref="SpellUsable.EffectiveRate"/>
/// models, and composes with it rather than feeding it: recovery divides, ACR multiplies. It applies both
/// to base cooldowns (at 100% an ability has no cooldown whatever its base duration) and to flat cooldown
/// reductions such as Rolling Flames (at 10% ACR a 1000ms reduction generates 900ms).
///
/// Sources combine additively. The gear-derived seed comes from the selected
/// <see cref="Combatant"/>, resolved at combatantinfo parse before any module exists, so there is no
/// module-ordering hazard; register further sources with <see cref="Add"/>. ACR is snapshot semantics:
/// <see cref="SpellUsable"/> reads <see cref="Current"/> when a cooldown starts and never rescales one in
/// flight, so a source that changed mid-fight would affect only cooldowns begun after the change.
/// </remarks>
public sealed partial class CooldownReduction : Analyzer
{
    private double _additional;

    /// <summary>
    /// Total Ability Cooldown Reduction as a fraction (0.12 = 12%), capped at 1.0 so a cooldown can be
    /// erased but never driven below zero.
    /// </summary>
    public double Current => Math.Min(1.0, Owner.SelectedCombatant.Stats.AbilityCooldownReduction + _additional);

    /// <summary>
    /// Registers Ability Cooldown Reduction from a source other than the gear seed, as a fraction
    /// (0.10 = 10%). Adds to the pool rather than multiplying with it.
    /// </summary>
    public void Add(double acr) => _additional += acr;

    /// <summary>
    /// Scales a cooldown duration by the current ACR: at 10% ACR a 30s cooldown becomes 27s. This applies to
    /// both base cooldowns at cast and flat cooldown reductions, so at 10% ACR a 1000ms Rolling Flames
    /// reduction generates 900ms.
    /// </summary>
    public int Scale(int milliseconds) => (int)(milliseconds * (1.0 - Current));
}
