using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public enum DotUpkeep
{
    Maintained,

    Erupting,
}

public sealed record MaraDot(Spell Cast, Spell Effect, DotUpkeep Upkeep)
{
    public int EffectId => Effect.FSLID;

    public string Name => Effect.Name;
}

public static class MaraDots
{
    public static IReadOnlyList<MaraDot> All { get; } =
    [
        new(Spells.WidowBite, Spells.WidowBitePoison, DotUpkeep.Maintained),
        new(Spells.HemorrhagingStrike, Spells.HemorrhagingStrikeBleed, DotUpkeep.Maintained),
        new(Spells.SkitteringBlades, Spells.SkitteringBladesPoison, DotUpkeep.Erupting),
    ];

    public static int Count => All.Count;

    public static MaraDot SeethingPoison { get; } = All[0];

    public static MaraDot Hemorrhage { get; } = All[1];

    public static MaraDot VolatilePoison { get; } = All[2];

    public static IReadOnlyList<MaraDot> Maintained { get; } = [.. All.Where(dot => dot.Upkeep == DotUpkeep.Maintained)];
}

public interface IMaraDotAnalyzer : IAnalyzerSurface;
