using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;

namespace FellowshipAnalyzer.Heroes.Ardeos.Guides;

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

    public static int Count => Fire.Length;

    public static string Series(ChartPalette palette, int slot) => palette.Resolve(Fire[slot % Fire.Length]);
}
