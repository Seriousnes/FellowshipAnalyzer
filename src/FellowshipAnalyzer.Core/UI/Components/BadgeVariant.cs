namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Colour scheme for a <see cref="Badge"/>. When set to <see cref="Custom"/>,
/// supply a CSS custom-property name via <c>Badge.CustomColorVar</c>.
/// </summary>
public enum BadgeVariant
{
    Neutral,
    Success,
    Failure,
    Tank,
    Healer,
    Dps,
    Gold,
    Custom,
}
