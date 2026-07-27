namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>Visual variant for <see cref="TipBox"/> callouts.</summary>
public enum TipBoxVariant
{
    /// <summary>Neutral informational callout.</summary>
    Info,

    /// <summary>A note from the analyzer author, distinct from an automated informational tip.</summary>
    Note,

    /// <summary>Positive callout confirming good play.</summary>
    Success,

    /// <summary>Cautionary callout for a common pitfall; renders with the "alert" ARIA role.</summary>
    Warning,

    /// <summary>Callout for a misplay or mistake; renders with the "alert" ARIA role.</summary>
    Error,
}
