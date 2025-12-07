namespace FellowshipAnalyzer.Components;

/// <summary>
/// One segment in a <see cref="StackedBar"/>.
/// </summary>
public sealed record StackedBarSegment(
    string Label,
    double Value,
    string Color,
    string? Tooltip = null);
