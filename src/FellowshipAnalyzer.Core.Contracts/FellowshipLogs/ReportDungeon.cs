namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents a single dungeon run within a report.
/// </summary>
public sealed record ReportDungeon(
    int Id,
    string Name,
    int EncounterId,
    bool? Kill,
    double StartTime,
    double EndTime,
    int? Difficulty,
    List<int>? FriendlyPlayers,
    double? CompletionPercentage,
    bool InProgress = false,
    List<DungeonPull>? DungeonPulls = null,
    List<DungeonNpc>? EnemyNpcs = null
)
{
    private const int ZoneEncounterOffset = 100_000;

    /// <summary>
    /// Icon URL for the dungeon/zone this dungeon took place in, served from the RPGLogs CDN, or
    /// <see langword="null"/> when the dungeon has no zone. Some reports offset the zone id by
    /// <c>100000</c>, so <see cref="EncounterId"/> is reduced modulo that offset before use.
    /// </summary>
    public string? DungeonIconUrl =>
        EncounterId % ZoneEncounterOffset is int zoneId and > 0
            ? $"https://assets.rpglogs.com/img/fellowship/bosses/{zoneId}-icon.jpg"
            : null;
}