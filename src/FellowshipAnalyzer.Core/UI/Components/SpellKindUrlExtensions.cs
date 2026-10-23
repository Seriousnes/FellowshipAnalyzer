using Fellowship.SDK;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.UI.Components;

internal static class SpellKindUrlExtensions
{
    /// <summary>
    /// The codex entity type for the native <paramref name="id"/> of <paramref name="kind"/>. A
    /// <see cref="SpellKind.Weapon"/> id names an augmentation, whose type the id selects.
    /// </summary>
    public static EntityType ToEntityType(this SpellKind kind, int id) => kind switch
    {
        SpellKind.Effect => EntityType.Effect,
        SpellKind.Talent => EntityType.Talent,
        SpellKind.Weapon when Augmentations.TypeOf(id) is { } augmentation => augmentation,
        _ => EntityType.Ability,
    };
}
