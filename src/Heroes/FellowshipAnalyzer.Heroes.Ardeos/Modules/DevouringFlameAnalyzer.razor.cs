using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Damage the Devouring Flame legendary (power 616) adds: an enemy takes 7% more damage from the player
/// for each Engulfing Flames instance active on it.
/// </summary>
[Dependency<Combatants>]
[ActiveWhen<HasDevouringFlame>]
public sealed partial class DevouringFlameAnalyzer : Analyzer
{
    /// <summary>Damage increase per active Engulfing Flames instance, as a fraction.</summary>
    public const double DamageIncreasePerInstance = 0.07;

    private double totalIncreasedDamage;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        var target = Combatants.GetCombatant(damageEvent.TargetId, damageEvent.TargetInstance);
        var stacks = target?.GetAuraInstanceCount(Spells.EngulfingFlamesDot, damageEvent.Timestamp) ?? 0;

        totalIncreasedDamage += CombatMath.CalculateEffectiveDamage(damageEvent, stacks * DamageIncreasePerInstance);
    }
}

/// <summary>Active when the selected combatant has the Devouring Flame legendary equipped.</summary>
public sealed class HasDevouringFlame : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => Equipped(context.SelectedCombatant);

    /// <summary>True when <paramref name="combatant"/> has the Devouring Flame legendary equipped.</summary>
    public static bool Equipped(FullCombatant combatant) => combatant.HasLegendary(Legendaries.DevouringFlame.Id);
}
