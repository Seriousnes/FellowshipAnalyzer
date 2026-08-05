using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public static class GundeDots
{
    public static Dot Rend { get; } = new(null, Spells.Rend, StackMethod.Stacking);

    public static IReadOnlyList<Dot> All { get; } = [Rend];

    public static int Count => All.Count;
}
