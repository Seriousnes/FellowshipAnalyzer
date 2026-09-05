namespace FellowshipAnalyzer.Core.UI.Charts;

/// <summary>
/// One aura's contribution to an <see cref="AuraStackGraph"/>: how many windows of it were open at each
/// sample. <see cref="Values"/> is index-aligned to the graph's sample grid.
/// </summary>
/// <param name="Label">The aura's name, shown in the legend and the tooltip.</param>
/// <param name="Color">The concrete CSS colour, resolved from a design token via <see cref="ChartPalette"/>.</param>
/// <param name="Values">Open windows at each sample, index-aligned to the graph's samples.</param>
public sealed record AuraStackBand(string Label, string Color, List<int> Values);

/// <summary>
/// A set of casts marked on an <see cref="AuraStackGraph"/>, plotted against the same sample grid as the
/// bands so a marker sits exactly at the stack count at the cast.
/// </summary>
/// <param name="Label">The marker set's name, shown in the legend.</param>
/// <param name="Color">The concrete CSS colour, resolved from a design token via <see cref="ChartPalette"/>.</param>
/// <param name="Values">The stack count to plot each marker at, or null at samples this set has no cast on.</param>
public sealed record AuraStackMarkers(string Label, string Color, List<int?> Values);
