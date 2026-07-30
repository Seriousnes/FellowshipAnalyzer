using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Guides;

internal static class MaraChartColors
{
    public static SeriesTokens Dots { get; } = new(
        nameof(FaPalette.Nature),
        nameof(FaPalette.Blood),
        nameof(FaPalette.Arcane));

    public static string Dot(ChartPalette palette, Dot dot)
    {
        for (var slot = 0; slot < MaraDots.Count; slot++)
        {
            if (MaraDots.All[slot].EffectId == dot.EffectId)
                return Dots.Series(palette, slot);
        }
        return Dots.Series(palette, 0);
    }
}
