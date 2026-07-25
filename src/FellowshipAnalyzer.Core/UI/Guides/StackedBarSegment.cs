namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// One segment in a <see cref="StackedBar"/>.
/// </summary>
/// <param name="Label">The segment's name in the legend.</param>
/// <param name="Value">The segment's share of the bar.</param>
/// <param name="Color">Design token reference for the segment's fill, for example <c>FaVar.PerfGood</c>.</param>
/// <param name="Tooltip">Optional hover text; the label and value are used when it is null.</param>
public sealed record StackedBarSegment(
    string Label,
    double Value,
    string Color,
    string? Tooltip = null);
