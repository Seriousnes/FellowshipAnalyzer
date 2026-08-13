namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// One spawn of a hostile unit, tracked for its aura history. Seeded from the rosters the report
/// declares - the dungeon's enemy NPC list and every dungeon pull's - and fabricated on demand for a
/// unit no roster names. <see cref="Analysis.Enemies"/> answers the enemy-facing questions over these.
/// </summary>
public sealed class Enemy(int id, int? instance, bool rostered = false) : Combatant(id)
{
    /// <summary>The spawn instance that distinguishes this unit from other units sharing <see cref="Combatant.Id"/>, or <c>null</c> when there is only one spawn.</summary>
    public int? Instance { get; } = instance;

    /// <summary>
    /// Whether a roster the report declares names this spawn. A unit first seen in the event stream
    /// is not rostered, and <see cref="Analysis.Enemies"/> admits it to a pull's population from the
    /// event naming it rather than from the pull's start.
    /// </summary>
    public bool Rostered { get; } = rostered;
}
