namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Colour scheme for a <see cref="Badge"/>. When set to <see cref="Custom"/>,
/// supply a CSS custom-property name via <c>Badge.CustomColorVar</c>.
/// </summary>
public enum BadgeVariant
{
    /// <summary>Neutral, muted colour with no semantic meaning attached.</summary>
    Neutral,
    /// <summary>Positive/passing semantic colour, e.g. a met threshold or a good result.</summary>
    Success,
    /// <summary>Negative/failing semantic colour, e.g. a missed threshold or an error.</summary>
    Failure,
    /// <summary>Tank role colour.</summary>
    Tank,
    /// <summary>Healer role colour.</summary>
    Healer,
    /// <summary>DPS role colour.</summary>
    Dps,
    /// <summary>Gold accent colour, used for highlight or premium emphasis.</summary>
    Gold,
    /// <summary>Caller-supplied colour, sourced from <c>Badge.CustomColorVar</c> instead of a fixed semantic token.</summary>
    Custom,
}
