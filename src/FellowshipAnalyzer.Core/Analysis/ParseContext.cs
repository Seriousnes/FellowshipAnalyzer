using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Immutable per-analysis context exposing only the values a module reasonably needs at construction
/// time: the selected player, the fight being analyzed, and report-level actor names. Populated by
/// <see cref="CombatLogParser.Analyze"/> and read by generator-emitted <c>CreateInstance</c>.
/// </summary>
public sealed record ParseContext(
    int PlayerId,
    ReportFight Fight,
    IReadOnlyDictionary<int, string> ActorNames,
    Combatant SelectedCombatant)
{
    public int FightStartTime => (int)Fight.StartTime;
    public int FightEndTime => (int)Fight.EndTime;
}
