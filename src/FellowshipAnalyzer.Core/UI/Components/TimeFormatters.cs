namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Shared JavaScript formatter snippets used by chart libraries (e.g. ApexCharts) to render
/// fight-relative timestamps. Centralised here so the same literal isn't duplicated across
/// chart components.
/// </summary>
public static class TimeFormatters
{
    /// <summary>
    /// JS formatter that converts a millisecond value into <c>m:ss</c> (e.g. <c>3:07</c>).
    /// Suitable for ApexCharts <c>Xaxis.Labels.Formatter</c> and <c>Tooltip.X.Formatter</c>.
    /// </summary>
    public const string MmSsJs =
        "function(val) { var s = Math.floor(val / 1000); return Math.floor(s / 60) + ':' + (s % 60 < 10 ? '0' : '') + (s % 60); }";

    /// <summary>
    /// JS formatter that returns a numeric value unchanged. Useful for ApexCharts
    /// <c>Tooltip.Y.Formatter</c> when the default decimal/thousands formatting is unwanted.
    /// </summary>
    public const string IdentityJs = "function(val) { return val; }";
}
