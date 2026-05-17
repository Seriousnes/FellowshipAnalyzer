namespace FellowshipAnalyzer.Api.Core.Caching;

/// <summary>
/// Read result from the persistent (blob) cache.
/// Callers must dispose <see cref="Content"/> after reading.
/// </summary>
public sealed record PersistentCacheEntry(
    /// <summary>The blob content stream. Caller is responsible for disposal.</summary>
    Stream Content,
    /// <summary>Length of the content in bytes as reported by the blob service.</summary>
    long ContentLength,
    /// <summary>Absolute expiry from the blob metadata, or <c>null</c> if not set.</summary>
    DateTimeOffset? ExpiresAt,
    /// <summary>Content-Encoding stored on the blob (e.g. <c>"gzip"</c>), or <c>null</c>.</summary>
    string? ContentEncoding,
    /// <summary>All non-TTL metadata stored on the blob.</summary>
    IReadOnlyDictionary<string, string> Metadata
);

/// <summary>
/// Options for a persistent cache write.
/// </summary>
public sealed record PersistentCacheWriteOptions(
    /// <summary>Absolute UTC time after which the entry is considered expired (lazy-expired on read).</summary>
    DateTimeOffset? ExpiresAt,
    /// <summary>MIME type to store on the blob, e.g. <c>"application/json"</c>.</summary>
    string? ContentType,
    /// <summary>Content-Encoding to store on the blob, e.g. <c>"gzip"</c>.</summary>
    string? ContentEncoding,
    /// <summary>Additional metadata key-value pairs to store alongside the TTL metadata.</summary>
    IReadOnlyDictionary<string, string>? Metadata = null
);

/// <summary>
/// Partition-keyed, stream-oriented persistent cache backed by Azure Blob Storage.
/// Sits between the in-process <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> (L1)
/// and the upstream FellowshipLogs API (L3).
/// </summary>
public interface IPersistentCache
{
    /// <summary>
    /// Retrieves a cached entry. Returns <c>null</c> on miss or if the entry has expired.
    /// Expired entries are lazily deleted from storage.
    /// </summary>
    ValueTask<PersistentCacheEntry?> GetAsync(CachePartition partition, string key, CancellationToken ct);

    /// <summary>
    /// Writes bytes to the cache. Prefer <see cref="SetStreamAsync"/> for large payloads.
    /// </summary>
    ValueTask SetAsync(CachePartition partition, string key, ReadOnlyMemory<byte> bytes, PersistentCacheWriteOptions options, CancellationToken ct);

    /// <summary>
    /// Writes the content of <paramref name="payload"/> to the cache without buffering the entire
    /// payload in memory. The stream is read once and must be positioned at the start.
    /// </summary>
    ValueTask SetStreamAsync(CachePartition partition, string key, Stream payload, PersistentCacheWriteOptions options, CancellationToken ct);

    /// <summary>
    /// Removes an entry from the cache if it exists.
    /// </summary>
    ValueTask RemoveAsync(CachePartition partition, string key, CancellationToken ct);
}
