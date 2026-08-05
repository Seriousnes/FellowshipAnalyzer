using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public static class MaraDots
{
    public static IReadOnlyList<Dot> All { get; } =
    [
        new(Spells.WidowBite, Spells.WidowBitePoison, StackMethod.Refresh),
        new(Spells.HemorrhagingStrike, Spells.HemorrhagingStrikeBleed, StackMethod.Refresh),
        new(Spells.SkitteringBlades, Spells.SkitteringBladesPoison, StackMethod.Refresh),
    ];

    public static int Count => All.Count;

    public static Dot SeethingPoison { get; } = All[0];

    public static Dot Hemorrhage { get; } = All[1];

    public static Dot VolatilePoison { get; } = All[2];

    public static IReadOnlyList<Dot> Maintained { get; } = [SeethingPoison, Hemorrhage];
}

public interface IMaraDotAnalyzer : IAnalyzerSurface;
