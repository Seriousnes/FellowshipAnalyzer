namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Metadata for a previously analyzed dungeon, used for report history display.
/// </summary>
public sealed record ReportHistoryEntry(
    string ReportCode,
    int DungeonId,
    int PlayerId,
    string? DungeonName,
    string? PlayerName,
    Analysis.HeroName? Hero,
    DateTimeOffset CachedAt
);

/// <summary>
/// Provides caching for combat event data and report history.
/// Implementations store events locally (e.g., IndexedDB) to avoid re-fetching completed dungeons.
/// In-progress dungeons must never be cached.
/// </summary>
public interface IReportCacheService
{
    /// <summary>
    /// Returns the raw UTF-8 events JSON bytes for a previously cached dungeon, or null on cache miss.
    /// </summary>
    ValueTask<byte[]?> GetCachedEventsBytesAsync(string reportCode, int dungeonId, int playerId);

    /// <summary>
    /// Caches the raw UTF-8 events JSON bytes for a completed dungeon and records it in history.
    /// Must only be called when <c>inProgress = false</c>.
    /// </summary>
    /// <param name="entry">History metadata for the dungeon.</param>
    /// <param name="eventsJsonBytes">Raw UTF-8 JSON bytes of the events response.</param>
    /// <param name="expiresAt">
    /// Optional UTC expiry from the server's <c>X-FellowshipAnalyzer-ExpiresAt</c> response header.
    /// When provided, the cache entry will be considered stale after this time and re-fetched.
    /// </param>
    ValueTask CacheAsync(ReportHistoryEntry entry, byte[] eventsJsonBytes, DateTimeOffset? expiresAt = null);

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
