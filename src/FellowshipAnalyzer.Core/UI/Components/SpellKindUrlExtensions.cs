using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>Maps a <see cref="SpellKind"/> to the fellows.gg URL path segment for that entity type.</summary>
internal static class SpellKindUrlExtensions
{
    public static string ToUrlSegment(this SpellKind kind) => kind switch
    {
        SpellKind.Effect => "effect",
        SpellKind.Talent => "talent",
        SpellKind.Weapon => "weapon",
        _ => "ability",
    };
}
