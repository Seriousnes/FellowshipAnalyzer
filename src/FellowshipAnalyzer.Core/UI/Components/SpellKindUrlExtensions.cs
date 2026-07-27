using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.UI.Components;

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
