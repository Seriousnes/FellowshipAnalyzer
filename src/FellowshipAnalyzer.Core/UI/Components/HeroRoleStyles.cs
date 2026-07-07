using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Maps a <see cref="HeroRole"/> to the CSS custom property that represents its accent colour.
/// Authoritative values live in <c>_tokens.scss</c> / <c>app.scss</c> as <c>--fa-role-*</c>.
/// </summary>
public static class HeroRoleStyles
{
    /// <summary>
    /// Returns a CSS <c>var(--fa-role-*)</c> expression for the role's accent colour,
    /// suitable for use in inline <c>style</c> attributes (e.g. <c>border-left-color</c>).
    /// </summary>
    public static string GetAccentColorVar(HeroRole role) => role switch
    {
        HeroRole.Tank => "var(--fa-role-tank)",
        HeroRole.Healer => "var(--fa-role-healer)",
        HeroRole.Dps => "var(--fa-role-dps)",
        _ => "var(--fa-role-unknown)",
    };
}
