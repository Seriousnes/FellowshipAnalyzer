namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A non-selected combat unit tracked purely for its aura history. Fabricated on demand when a
/// player-sourced aura event targets a unit that has no <see cref="FellowshipAnalyzer.Core.Events.CombatantInfoEvent"/>.
/// </summary>
public sealed class Enemy(int id, int? instance) : Entity
{
    /// <summary>The actor id this unit was fabricated from.</summary>
    public int Id { get; } = id;

    /// <summary>The spawn instance that distinguishes this unit from other units sharing <see cref="Id"/>, or <c>null</c> when there is only one spawn.</summary>
    public int? Instance { get; } = instance;
}
