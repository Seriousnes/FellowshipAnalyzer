using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>What a Mara damage-over-time window is worth, which decides whether reapplying it is upkeep or waste.</summary>
public enum DotUpkeep
{
    /// <summary>The window pays while it stands, so it is kept rolling and reapplying before it falls off is correct play.</summary>
    Maintained,

    /// <summary>The window pays when it expires, so reapplying it early cancels the payout it was carrying.</summary>
    Erupting,
}

/// <summary>
/// One of Mara's poisons or bleeds: the ability that lays it, the debuff it leaves on the enemy, and
/// whether that debuff is meant to be kept rolling or left to run out.
/// </summary>
/// <param name="Cast">The ability the player presses to lay the effect.</param>
/// <param name="Effect">The debuff tracked on the enemy, which is what apply, refresh and remove events carry.</param>
/// <param name="Upkeep">Whether reapplying the window is upkeep or throws its payout away.</param>
public sealed record MaraDot(Spell Cast, Spell Effect, DotUpkeep Upkeep)
{
    /// <summary>The debuff id to match apply, refresh and remove events by.</summary>
    public int EffectId => Effect.FSLID;

    /// <summary>The debuff's own name, which is how the effect is labelled throughout the guides.</summary>
    public string Name => Effect.Name;
}

/// <summary>
/// The poisons and bleeds Mara lays on enemies, the shared reference every Mara analyzer that reasons
/// about damage-over-time upkeep reads. The order is the order guides present them in and the order
/// chart series colours are assigned in, so an effect wears one colour and one position everywhere.
/// </summary>
public static class MaraDots
{
    /// <summary>Every tracked poison and bleed, in presentation order.</summary>
    public static IReadOnlyList<MaraDot> All { get; } =
    [
        new(Spells.WidowBite, Spells.WidowBitePoison, DotUpkeep.Maintained),
        new(Spells.HemorrhagingStrike, Spells.HemorrhagingStrikeBleed, DotUpkeep.Maintained),
        new(Spells.SkitteringBlades, Spells.SkitteringBladesPoison, DotUpkeep.Erupting),
    ];

    /// <summary>How many poisons and bleeds a complete setup lays.</summary>
    public static int Count => All.Count;

    /// <summary>The sixty-second poison a stealth-opened Widow's Bite leaves on the priority target.</summary>
    public static MaraDot SeethingPoison { get; } = All[0];

    /// <summary>The bleed Hemorrhaging Strike leaves, whose duration is bought with the combo points spent on the cast.</summary>
    public static MaraDot Hemorrhage { get; } = All[1];

    /// <summary>The poison a stealth-empowered Skittering Blades leaves, which erupts for area damage as it expires.</summary>
    public static MaraDot VolatilePoison { get; } = All[2];

    /// <summary>The effects kept rolling on a target, in presentation order.</summary>
    public static IReadOnlyList<MaraDot> Maintained { get; } = [.. All.Where(dot => dot.Upkeep == DotUpkeep.Maintained)];
}

/// <summary>
/// Shared surface for Mara's poison and bleed analysis. <see cref="MaraDotUptimeAnalyzer"/> (boss pulls)
/// and <see cref="MaraDotSpreadAnalyzer"/> (multi-target pulls) each implement this marker so both are
/// exposed under one <c>Parser.MaraDotAnalyzers</c> stream and <c>pull.MaraDotAnalyzer</c> accessor,
/// without sharing a base class. The two analyses share no members; consumers switch on the concrete type.
/// </summary>
public interface IMaraDotAnalyzer : IAnalyzerSurface;
