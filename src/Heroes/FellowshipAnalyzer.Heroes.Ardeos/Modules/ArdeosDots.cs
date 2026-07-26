using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>How a damage-over-time effect's presence reads beyond simply standing or not.</summary>
public enum DotMagnitude
{
    /// <summary>One window per enemy and no stacks, so presence is the whole story.</summary>
    Presence,

    /// <summary>Each application opens its own window and they tick concurrently.</summary>
    Instances,

    /// <summary>One window that accumulates stacks.</summary>
    Stacks,
}

/// <summary>
/// One of Ardeos's fire damage-over-time effects: the ability the player casts, the aura it leaves on
/// the enemy, and how that aura's magnitude reads.
/// </summary>
/// <param name="Cast">The ability the player casts. It names, icons and links the effect in the UI, so
/// Apocalypse reads as "Apocalypse" rather than the aura's own "Apocalypse: Explosivo".</param>
/// <param name="Effect">The aura tracked on the enemy, which is what a DoT query matches.</param>
/// <param name="Magnitude">Whether the effect stacks, layers concurrent instances, or simply stands.</param>
public sealed record ArdeosDot(Spell Cast, Spell Effect, DotMagnitude Magnitude)
{
    /// <summary>The aura id to query the tracked enemy debuff by.</summary>
    public int EffectId => Effect.FSLID;

    /// <summary>The ability's name, which is how the effect is labelled throughout the guides.</summary>
    public string Name => Cast.Name;
}

/// <summary>
/// The six fire damage-over-time effects Ardeos's rotation lays on enemies, the shared reference every
/// Ardeos analyzer that reasons about DoT coverage reads. The order is the order guides present them in
/// and the order chart series colours are assigned in, so a DoT wears one colour and one position
/// everywhere.
/// </summary>
public static class ArdeosDots
{
    /// <summary>Every tracked damage-over-time effect, in presentation order.</summary>
    public static IReadOnlyList<ArdeosDot> All { get; } =
    [
        new(Spells.EngulfingFlames, Spells.EngulfingFlamesDot, DotMagnitude.Instances),
        new(Spells.SearingBlaze, Spells.SearingBlazeDot, DotMagnitude.Presence),
        new(Spells.FireFrogs, Spells.FireFrogsDot, DotMagnitude.Presence),
        new(Spells.Incinerate, Spells.IncinerateDot, DotMagnitude.Stacks),
        new(Spells.FireBall, Spells.FireBallDot, DotMagnitude.Presence),
        new(Spells.Apocalypse, Spells.ApocalypseDot, DotMagnitude.Presence),
    ];

    /// <summary>How many damage-over-time effects a complete setup lays.</summary>
    public static int Count => All.Count;

    /// <summary>The effect Engulfing Flames leaves, which layers concurrent instances.</summary>
    public static ArdeosDot EngulfingFlames { get; } = All[0];

    /// <summary>The effect Searing Blaze leaves.</summary>
    public static ArdeosDot SearingBlaze { get; } = All[1];

    /// <summary>The effect Fire Frogs leaves.</summary>
    public static ArdeosDot FireFrogs { get; } = All[2];

    /// <summary>The effect Incinerate leaves, which accumulates stacks.</summary>
    public static ArdeosDot Incinerate { get; } = All[3];

    /// <summary>The effect Fire Ball leaves.</summary>
    public static ArdeosDot FireBall { get; } = All[4];

    /// <summary>The effect Apocalypse leaves.</summary>
    public static ArdeosDot Apocalypse { get; } = All[5];
}
