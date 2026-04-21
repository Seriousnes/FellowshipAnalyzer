namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Metadata for a previously analyzed fight, used for report history display.
/// </summary>
public sealed record ReportHistoryEntry(
    string ReportCode,
    int FightId,
    int PlayerId,
    string? FightName,
    string? PlayerName,
    string? HeroId,
    DateTimeOffset CachedAt
);

/// <summary>
/// Provides caching for combat event data and report history.
/// Implementations store events locally (e.g., IndexedDB) to avoid re-fetching completed fights.
/// In-progress fights must never be cached.
/// </summary>
public interface IReportCacheService
{
    /// <summary>
    /// Returns the raw serialized events JSON for a previously cached fight, or null on cache miss.
    /// </summary>
    ValueTask<string?> GetCachedEventsJsonAsync(string reportCode, int fightId, int playerId);

    /// <summary>
    /// Caches the serialized events JSON for a completed fight and records it in history.
    /// Must only be called when <c>inProgress = false</c>.
    /// </summary>
    ValueTask CacheAsync(ReportHistoryEntry entry, string eventsJson);

    /// <summary>
    /// Returns all history entries, newest first.
    /// </summary>
    ValueTask<IReadOnlyList<ReportHistoryEntry>> GetHistoryAsync();

    /// <summary>
    /// Returns the raw serialized master data JSON for a report, or null on cache miss.
    /// </summary>
    ValueTask<string?> GetCachedMasterDataJsonAsync(string reportCode);

    /// <summary>
    /// Caches the serialized master data JSON for a report.
    /// </summary>
    ValueTask CacheMasterDataAsync(string reportCode, string masterDataJson);
}
