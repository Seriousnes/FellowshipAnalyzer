using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

namespace FellowshipAnalyzer.Heroes.Ardeos.Core;

public static class ArdeosDots
{
    public static List<Dot> All { get; } =
    [
        new(Spells.EngulfingFlames, Spells.EngulfingFlamesDot, StackMethod.ConcurrentInstances),
        new(Spells.SearingBlaze, Spells.SearingBlazeDot, StackMethod.Refresh),
        new(Spells.FireFrogs, Spells.FireFrogsDot, StackMethod.Refresh),
        new(Spells.Incinerate, Spells.IncinerateDot, StackMethod.Stacking),
        new(Spells.FireBall, Spells.FireBallDot, StackMethod.Accumulate),
        new(Spells.Apocalypse, Spells.ApocalypseDot, StackMethod.Refresh),
    ];

    public static int Count => All.Count;

    public static Dot EngulfingFlames { get; } = All[0];

    public static Dot SearingBlaze { get; } = All[1];

    public static Dot FireFrogs { get; } = All[2];

    public static Dot Incinerate { get; } = All[3];

    public static Dot FireBall { get; } = All[4];

    public static Dot Apocalypse { get; } = All[5];

    public static int IndexOf(Dot dot)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index] == dot)
                return index;
        }

        throw new ArgumentOutOfRangeException(nameof(dot), $"{dot.Effect.Name} is not an Ardeos damage-over-time effect.");
    }
}
