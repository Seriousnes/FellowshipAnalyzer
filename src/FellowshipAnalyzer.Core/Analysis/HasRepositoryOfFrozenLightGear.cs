using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Activation predicate for <see cref="RepositoryOfFrozenLightAnalyzer"/> on a combatant wearing their hero's weapon in <see cref="Items.RepositoryOfFrozenLightWeapons"/>.</summary>
public sealed class HasRepositoryOfFrozenLightGear : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has the <see cref="Items.RepositoryOfFrozenLightWeapons"/> weapon whose <see cref="Item.Hero"/> is the parser's hero equipped.</summary>
    public static bool IsActive(ParseContext context) =>
        context.Hero?.Name is { } hero
        && Items.RepositoryOfFrozenLightWeapons.Any(weapon => weapon.Hero == hero && context.SelectedCombatant.HasItem(weapon.Id));
}
