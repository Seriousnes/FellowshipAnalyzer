namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Identifies the actor an event applies to for declarative <see cref="OnAttribute{TEvent}"/> filters.
/// </summary>
/// <remarks>
/// Modelled as a flags enum (not record singletons) because attribute arguments must be
/// compile-time constants — only primitives, strings, types, and enum values qualify.
/// Reference-type singletons like <c>public static Actor Player { get; } = new PlayerActor();</c>
/// can't be written as <c>[On&lt;CastEvent&gt;(By = Actor.Player)]</c>. The generator
/// pattern-matches the enum value to emit specialized predicates.
/// </remarks>
[Flags]
public enum Actor
{
    /// <summary>Matches events whoever the actor is.</summary>
    Any = 0,
    /// <summary>Matches events where the player is the actor.</summary>
    Player = 1,
}
