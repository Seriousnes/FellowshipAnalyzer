using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Activation predicate for <see cref="RepositoryOfFrozenLightAnalyzer"/> on a selected combatant with a Repository of Frozen Light weapon equipped.</summary>
public sealed class HasRepositoryOfFrozenLightGear : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has a weapon granting Repository of Frozen Light equipped.</summary>
    public static bool IsActive(ParseContext context) => new Item[] { Items.FragmentsOfFrozenTime, Items.IceshapersBrand, Items.AncientIceshapersTome, }.Any(item => context.SelectedCombatant.HasItem(item.Id));
}
