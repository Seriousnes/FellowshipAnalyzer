using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

public enum DotMagnitude
{
    Presence,

    Instances,

    Stacks,
}

public sealed record ArdeosDot(Spell Cast, Spell Effect, DotMagnitude Magnitude)
{
    public int EffectId => Effect.FSLID;

    public string Name => Cast.Name;
}

public record ArdeosDotCoverage
{
    public required ArdeosDot Dot { get; init; }

    public required int Instances { get; init; }

    public required int Stacks { get; init; }

    public bool Active => Instances > 0;

    public int? Magnitude => Dot.Magnitude switch
    {
        DotMagnitude.Instances => Instances,
        DotMagnitude.Stacks => Stacks,
        _ => null,
    };
}

public static class ArdeosDots
{
    public static IReadOnlyList<ArdeosDot> All { get; } =
    [
        new(Spells.EngulfingFlames, Spells.EngulfingFlamesDot, DotMagnitude.Instances),
        new(Spells.SearingBlaze, Spells.SearingBlazeDot, DotMagnitude.Presence),
        new(Spells.FireFrogs, Spells.FireFrogsDot, DotMagnitude.Presence),
        new(Spells.Incinerate, Spells.IncinerateDot, DotMagnitude.Stacks),
        new(Spells.FireBall, Spells.FireBallDot, DotMagnitude.Presence),
        new(Spells.Apocalypse, Spells.ApocalypseDot, DotMagnitude.Presence),
    ];

    public static int Count => All.Count;

    public static ArdeosDot EngulfingFlames { get; } = All[0];

    public static ArdeosDot SearingBlaze { get; } = All[1];

    public static ArdeosDot FireFrogs { get; } = All[2];

    public static ArdeosDot Incinerate { get; } = All[3];

    public static ArdeosDot FireBall { get; } = All[4];

    public static ArdeosDot Apocalypse { get; } = All[5];
}
