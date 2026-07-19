namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A non-selected combat unit tracked purely for its aura history. Fabricated on demand when a
/// player-sourced aura event targets a unit that has no <see cref="Events.CombatantInfoEvent"/>.
/// </summary>
public sealed class Enemy(int id, int? instance) : Entity
{
    public int Id { get; } = id;
    public int? Instance { get; } = instance;
}
