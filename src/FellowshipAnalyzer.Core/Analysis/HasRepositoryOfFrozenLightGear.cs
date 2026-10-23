using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Activation predicate for <see cref="RepositoryOfFrozenLightAnalyzer"/> on an Aeona, Sylvie or Vigour combatant with item 5986 equipped. Item 5986 grants Repository of Frozen Light to those heroes only.</summary>
public sealed class HasRepositoryOfFrozenLightGear : IModuleActivePredicate
{
    /// <summary><c>true</c> when the parser's hero is Aeona, Sylvie or Vigour and the selected combatant has that hero's Repository of Frozen Light weapon equipped.</summary>
    public static bool IsActive(ParseContext context) =>
        WeaponFor(context.Hero?.Name) is { } weapon && context.SelectedCombatant.HasItem(weapon.Id);

    private static Item? WeaponFor(HeroName? hero) => hero switch
    {
        HeroName.Aeona => Items.FragmentsOfFrozenTime,
        HeroName.Sylvie => Items.IceshapersBrand,
        HeroName.Vigour => Items.AncientIceshapersTome,
        _ => null,
    };
}
