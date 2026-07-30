using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;

namespace FellowshipAnalyzer.Heroes.Ardeos.Core;

internal static class ArdeosChartColors
{
    public static SeriesTokens Dots { get; } = new(
        nameof(FaPalette.Fire1),
        nameof(FaPalette.Fire2),
        nameof(FaPalette.Fire3),
        nameof(FaPalette.Fire4),
        nameof(FaPalette.Fire5),
        nameof(FaPalette.Fire6));
}
