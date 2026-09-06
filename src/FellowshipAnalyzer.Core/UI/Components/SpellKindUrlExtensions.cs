using Fellowship.SDK;

using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.UI.Components;

internal static class SpellKindUrlExtensions
{
    public static EntityType ToEntityType(this SpellKind kind) => kind switch
    {
        SpellKind.Effect => EntityType.Effect,
        SpellKind.Talent => EntityType.Talent,
        SpellKind.Weapon => EntityType.Trait,
        _ => EntityType.Ability,
    };
}
