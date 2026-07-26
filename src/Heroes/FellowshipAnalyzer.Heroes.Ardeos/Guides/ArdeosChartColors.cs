using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;

namespace FellowshipAnalyzer.Heroes.Ardeos.Guides;

/// <summary>
/// The colours an Ardeos chart paints its series in. Ardeos burns, so the series read as a flame:
/// brightest at the foot of a stack, deepest at its crown.
/// </summary>
internal static class ArdeosChartColors
{
    private static readonly string[] Fire =
    [
        nameof(FaPalette.Fire1),
        nameof(FaPalette.Fire2),
        nameof(FaPalette.Fire3),
        nameof(FaPalette.Fire4),
        nameof(FaPalette.Fire5),
        nameof(FaPalette.Fire6),
    ];

    /// <summary>How many series the ramp colours before a slot wraps back to its first step.</summary>
    public static int Count => Fire.Length;

    /// <summary>The colour for a series slot, counted from zero.</summary>
    public static string Series(ChartPalette palette, int slot) => palette.Resolve(Fire[slot % Fire.Length]);
}
