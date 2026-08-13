using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Immutable per-analysis context exposing only the values a module reasonably needs at construction
/// time: the selected player, the fight being analyzed, and report-level actor data. Populated by
/// <see cref="CombatLogParser.Analyze"/> and read by generator-emitted <c>CreateInstance</c>.
/// </summary>
public sealed record ParseContext(
    int PlayerId,
    ReportFight Fight,
    IReadOnlyDictionary<int, string> ActorNames,
    FullCombatant SelectedCombatant,
    IReadOnlyList<ReportActor>? Actors = null)
{
    /// <summary>Report-level actor master data, empty when the host supplied none.</summary>
    public IReadOnlyList<ReportActor> Actors { get; init; } = Actors ?? [];

    /// <summary>The fight's start timestamp, truncated to <see cref="int"/> from <see cref="ReportFight.StartTime"/>.</summary>
    public int FightStartTime => (int)Fight.StartTime;

    /// <summary>The fight's end timestamp, truncated to <see cref="int"/> from <see cref="ReportFight.EndTime"/>.</summary>
    public int FightEndTime => (int)Fight.EndTime;

    /// <summary>The dungeon pulls Fellowship Logs recorded for <see cref="Fight"/>, if it exposes any.</summary>
    public IReadOnlyList<DungeonPull>? DungeonPulls => Fight.DungeonPulls;

    /// <summary>The enemy NPCs present on <see cref="Fight"/>, as reported by Fellowship Logs.</summary>
    public IReadOnlyList<FightNpc>? EnemyNpcs => Fight.EnemyNpcs;
}
