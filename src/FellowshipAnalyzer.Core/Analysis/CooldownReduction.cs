namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's Ability Cooldown Reduction (ACR) per ability: <c>effective = base * (1 - acr)</c>.
/// </summary>
/// <remarks>
/// ACR is a distinct mechanic from the cooldown recovery pool <see cref="SpellUsable.EffectiveRate"/>
/// models, and composes with it rather than feeding it: recovery divides, ACR multiplies. It applies both
/// to base cooldowns (at 100% an ability has no cooldown whatever its base duration) and to flat cooldown
/// reductions such as Rolling Flames (at 10% ACR a 1000ms reduction generates 900ms).
///
/// The pool is scoped per ability: an unscoped modifier applies to every ability, while a scoped modifier
/// applies only to the abilities its <see cref="CooldownScope"/> matches. The gear-derived seed comes from
/// the selected <see cref="Combatant"/>, resolved at combatantinfo parse before any module exists, so there
/// is no module-ordering hazard; register further sources with <see cref="Add"/>. ACR is snapshot semantics:
/// <see cref="SpellUsable"/> reads <see cref="Current"/> when a cooldown starts and never rescales one in
/// flight, so a source that changed mid-fight would affect only cooldowns begun after the change.
/// </remarks>
public sealed partial class CooldownReduction : Analyzer
{
    private readonly List<CooldownModifier> _additional = [];

    /// <summary>
    /// Total Ability Cooldown Reduction for <paramref name="ability"/> as a fraction (0.12 = 12%), summing the
    /// combatant seed and every registered modifier that applies to it, capped at 1.0 so a cooldown can be
    /// erased but never driven below zero. A <c>null</c> ability accrues only unscoped modifiers.
    /// </summary>
    public double Current(SpellbookAbility? ability)
    {
        var total = Owner.SelectedCombatant.Stats.AbilityCooldownReduction.Total(ability);
        foreach (var modifier in _additional)
        {
            if (modifier.Scope is null || (ability is not null && modifier.Scope.Matches(ability)))
                total += modifier.Value;
        }
        return Math.Min(1.0, total);
    }

    /// <summary>
    /// Registers Ability Cooldown Reduction from a source other than the gear seed. Adds to the pool rather
    /// than multiplying with it; an unscoped modifier applies to every ability, a scoped one only to the
    /// abilities its <see cref="CooldownScope"/> matches.
    /// </summary>
    public void Add(CooldownModifier modifier) => _additional.Add(modifier);

    /// <summary>
    /// Scales a cooldown duration by <paramref name="ability"/>'s current ACR: at 10% ACR a 30s cooldown
    /// becomes 27s. This applies to both base cooldowns at cast and flat cooldown reductions, so at 10% ACR a
    /// 1000ms Rolling Flames reduction generates 900ms.
    /// </summary>
    public int Scale(SpellbookAbility? ability, int milliseconds) => (int)(milliseconds * (1.0 - Current(ability)));
}
