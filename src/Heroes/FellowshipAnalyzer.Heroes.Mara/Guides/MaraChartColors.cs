using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Guides;

internal static class MaraChartColors
{
    private static readonly string[] Slots =
    [
        nameof(FaPalette.Nature),
        nameof(FaPalette.Blood),
        nameof(FaPalette.Arcane),
    ];

    public static int Count => Slots.Length;

    public static string Series(ChartPalette palette, int slot) => palette.Resolve(Slots[slot % Slots.Length]);

    public static string Dot(ChartPalette palette, MaraDot dot)
    {
        for (var slot = 0; slot < MaraDots.Count; slot++)
        {
            if (MaraDots.All[slot].EffectId == dot.EffectId)
                return Series(palette, slot);
        }
        return Series(palette, 0);
    }
}
