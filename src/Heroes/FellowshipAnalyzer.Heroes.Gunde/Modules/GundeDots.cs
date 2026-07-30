using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public static class GundeDots
{
    /// <summary>Several abilities apply the bleed, so it carries no single cast.</summary>
    public static Dot Rend { get; } = new(null, Spells.Rend, StackMethod.Stacking);

    public static IReadOnlyList<Dot> All { get; } = [Rend];

    public static int Count => All.Count;
}
