using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Guides;

/// <summary>
/// The colours a Mara chart paints its series in. Mara poisons and bleeds, so each slot wears what it
/// leaves behind: venom green for the Seething Poison, red for the Hemorrhage bleed, and violet for the
/// Volatile Poison's eruption.
/// </summary>
internal static class MaraChartColors
{
    private static readonly string[] Slots =
    [
        nameof(FaPalette.Nature),
        nameof(FaPalette.Blood),
        nameof(FaPalette.Arcane),
    ];

    /// <summary>How many series the ramp colours before a slot wraps back to its first step.</summary>
    public static int Count => Slots.Length;

    /// <summary>The colour for a series slot, counted from zero.</summary>
    public static string Series(ChartPalette palette, int slot) => palette.Resolve(Slots[slot % Slots.Length]);

    /// <summary>The colour an effect wears everywhere, taken from its position in <see cref="MaraDots.All"/>.</summary>
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
