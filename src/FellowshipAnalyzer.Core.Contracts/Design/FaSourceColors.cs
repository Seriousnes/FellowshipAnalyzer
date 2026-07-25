namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// The two source ramps the Dark and Light themes are built from: the warm-gold <c>wg</c> cream
/// ramp and the blue-steel <c>bs</c> ramp. They are inputs to the themes, not tokens, so nothing
/// outside this folder names them.
/// </summary>
internal static class FaSourceColors
{
    internal static readonly FaColor WgCream = "#fffaf1";
    internal static readonly FaColor WgPale = "#edddb8";
    internal static readonly FaColor WgLight = "#dcc080";
    internal static readonly FaColor WgGold = "#caa347";
    internal static readonly FaColor WgDeep = "#b8860e";

    internal static readonly FaColor BsSky = "#7ab3d0";
    internal static readonly FaColor BsBlue = "#3383b1";
    internal static readonly FaColor BsNavy = "#2a4677";
    internal static readonly FaColor BsGrey = "#989a9f";
    internal static readonly FaColor BsSilver = "#b3b6bb";
    internal static readonly FaColor BsPearl = "#cdcdcc";
}

/// <summary>
/// The alpha steps every tint in the design system is drawn from, expressed as a fraction of 1.
/// Mirrors the tint scale in <c>_tokens.scss</c>.
/// </summary>
internal static class FaTint
{
    internal const double None = 0d;
    internal const double Faint = 0.06d;
    internal const double Subtle = 0.12d;
    internal const double Soft = 0.20d;
    internal const double Muted = 0.30d;
    internal const double Medium = 0.40d;
    internal const double Strong = 0.60d;
    internal const double Veil = 0.80d;
}
