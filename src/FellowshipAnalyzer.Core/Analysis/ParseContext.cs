using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Immutable per-analysis context exposing only the values a module reasonably needs at construction
/// time: the selected player, the dungeon being analyzed, and report-level actor data. Populated by
/// <see cref="CombatLogParser.Analyze"/> and read by generator-emitted <c>CreateInstance</c>.
/// </summary>
public sealed record ParseContext(
    int PlayerId,
    ReportDungeon Dungeon,
    Dictionary<int, string> ActorNames,
    FullCombatant SelectedCombatant,
    List<ReportActor>? Actors = null)
{
    /// <summary>Report-level actor master data, empty when the host supplied none.</summary>
    public List<ReportActor> Actors { get; init; } = Actors ?? [];

    /// <summary>The dungeon's start timestamp, truncated to <see cref="int"/> from <see cref="ReportDungeon.StartTime"/>.</summary>
    public int DungeonStartTime => (int)Dungeon.StartTime;

    /// <summary>The dungeon's end timestamp, truncated to <see cref="int"/> from <see cref="ReportDungeon.EndTime"/>.</summary>
    public int DungeonEndTime => (int)Dungeon.EndTime;

    /// <summary>The dungeon pulls for <see cref="Dungeon"/>, if it exposes any.</summary>
    public List<DungeonPull>? DungeonPulls => Dungeon.DungeonPulls;

    /// <summary>The enemy NPCs present on <see cref="Dungeon"/>.</summary>
    public List<DungeonNpc>? EnemyNpcs => Dungeon.EnemyNpcs;
}
