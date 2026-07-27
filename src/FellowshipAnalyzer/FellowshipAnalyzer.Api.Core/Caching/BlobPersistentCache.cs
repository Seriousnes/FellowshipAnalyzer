using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Api.Core.Caching;

/// <summary>
/// Azure Blob Storage-backed <see cref="IPersistentCache"/>.
/// Containers are created lazily on first use per partition.
/// TTL is stored as ISO-8601 blob metadata (<c>expiresAt</c>) and evaluated on read;
/// expired entries are lazily deleted.
/// </summary>
public sealed class BlobPersistentCache : IPersistentCache
{
    private const string ExpiresAtMetadataKey = "expiresAt";

    private readonly BlobServiceClient _serviceClient;
    private readonly BlobPersistentCacheOptions _options;
    private readonly ILogger<BlobPersistentCache> _logger;

    private readonly Dictionary<CachePartition, Lazy<Task<BlobContainerClient>>> _containers;

    public BlobPersistentCache(BlobServiceClient serviceClient, BlobPersistentCacheOptions options, ILogger<BlobPersistentCache> logger)
    {
        _serviceClient = serviceClient;
        _options = options;
        _logger = logger;
        _containers = new Dictionary<CachePartition, Lazy<Task<BlobContainerClient>>>
        {
            [CachePartition.GameData] = MakeLazy(CachePartition.GameData),
            [CachePartition.Metadata] = MakeLazy(CachePartition.Metadata),
            [CachePartition.Events] = MakeLazy(CachePartition.Events),
        };
    }

    /// <inheritdoc />
    public async ValueTask<PersistentCacheEntry?> GetAsync(CachePartition partition, string key, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Blob GET partition={Partition} key={Key} starting", partition, key);
        var container = await GetContainerAsync(partition);
        _logger.LogInformation("Blob GET partition={Partition} key={Key} container ready t={ElapsedMs}ms", partition, key, sw.ElapsedMilliseconds);
        var blob = container.GetBlobClient(key);

        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
            var details = response.Value.Details;
            _logger.LogInformation(
                "Blob GET partition={Partition} key={Key} download headers received length={Length} encoding={Encoding} t={ElapsedMs}ms",
                partition, key, details.ContentLength, details.ContentEncoding, sw.ElapsedMilliseconds);

            if (details.Metadata.TryGetValue(ExpiresAtMetadataKey, out var expiresAtStr)
                && DateTimeOffset.TryParseExact(
                    expiresAtStr, "O",
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var expiresAt)
                && DateTimeOffset.UtcNow >= expiresAt)
            {
                await response.Value.Content.DisposeAsync();
                _ = DeleteExpiredAsync(blob);
                return null;
            }

            var parsedExpiry = details.Metadata.TryGetValue(ExpiresAtMetadataKey, out var s)
                && DateTimeOffset.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var exp)
                ? exp : (DateTimeOffset?)null;

            var metadata = details.Metadata
                .Where(kvp => !string.Equals(kvp.Key, ExpiresAtMetadataKey, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            return new PersistentCacheEntry(
                Content: response.Value.Content,
                ContentLength: details.ContentLength,
                ExpiresAt: parsedExpiry,
                ContentEncoding: string.IsNullOrEmpty(details.ContentEncoding) ? null : details.ContentEncoding,
                Metadata: metadata);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Blob GET partition={Partition} key={Key} 404 miss t={ElapsedMs}ms", partition, key, sw.ElapsedMilliseconds);
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(
        CachePartition partition, string key,
        ReadOnlyMemory<byte> bytes,
        PersistentCacheWriteOptions options,
        CancellationToken ct)
    {
        var container = await GetContainerAsync(partition);
        var blob = container.GetBlobClient(key);

        Stream stream;
        if (MemoryMarshal.TryGetArray(bytes, out var segment))
        {
            stream = new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false);
        }
        else
        {
            stream = new MemoryStream(bytes.ToArray(), writable: false);
        }

        await using (stream)
        {
            await UploadAsync(blob, stream, options, ct);
        }

        _logger.LogInformation(
            "Blob SET partition={Partition} key={Key} bytes={Bytes} committed",
            partition, key, bytes.Length);
    }

    /// <inheritdoc />
    public async ValueTask SetStreamAsync(
        CachePartition partition, string key,
        Stream payload,
        PersistentCacheWriteOptions options,
        CancellationToken ct)
    {
        var container = await GetContainerAsync(partition);
        var blob = container.GetBlobClient(key);
        await UploadAsync(blob, payload, options, ct);

        _logger.LogInformation(
            "Blob SET partition={Partition} key={Key} committed (stream)",
            partition, key);
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(CachePartition partition, string key, CancellationToken ct)
    {
        var container = await GetContainerAsync(partition);
        var blob = container.GetBlobClient(key);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }

    private static async Task UploadAsync(BlobClient blob, Stream content, PersistentCacheWriteOptions options, CancellationToken ct)
    {
        var metadata = new Dictionary<string, string>();
        if (options.ExpiresAt.HasValue)
        {
            metadata[ExpiresAtMetadataKey] = options.ExpiresAt.Value.ToString("O");
        }
        if (options.Metadata is not null)
        {
            foreach (var kvp in options.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = options.ContentType,
                ContentEncoding = options.ContentEncoding,
            },
            Metadata = metadata,
        }, ct);
    }

    private Task<BlobContainerClient> GetContainerAsync(CachePartition partition)
        => _containers[partition].Value;

    private Lazy<Task<BlobContainerClient>> MakeLazy(CachePartition partition)
        => new(() => CreateContainerAsync(_options.ContainerName(partition)));

    private async Task<BlobContainerClient> CreateContainerAsync(string containerName)
    {
        var container = _serviceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();
        return container;
    }

    private static async Task DeleteExpiredAsync(BlobClient blob)
    {
        try
        {
            await blob.DeleteIfExistsAsync();
        }
        catch
        {
        }
    }
}
