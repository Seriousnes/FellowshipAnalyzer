using System;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[Dependency<Combatants>]
[ActiveWhen<DraconicBracersoftheDevouringFlameActivePredicate>]
public sealed partial class DraconicBracersoftheDevouringFlame : Analyzer
{
    private const double DamageAmpPerStack = 0.07;
    private double totalIncreasedDamage = 0.0;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        var target = Combatants.GetCombatant(damageEvent.TargetId, damageEvent.TargetInstance);
        var stacks = target?.GetAuraInstanceCount(Spells.EngulfingFlamesDot, damageEvent.Timestamp) ?? 0;

        totalIncreasedDamage += CombatMath.CalculateEffectiveDamage(damageEvent, stacks * DamageAmpPerStack);
    }
}

internal class DraconicBracersoftheDevouringFlameActivePredicate : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => context.SelectedCombatant.HasItem(Items.DraconicBracersOfTheDevouringFlame.Id);
}