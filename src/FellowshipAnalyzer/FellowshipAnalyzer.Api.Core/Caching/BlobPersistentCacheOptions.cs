namespace FellowshipAnalyzer.Api.Core.Caching;

/// <summary>
/// Configuration for <see cref="BlobPersistentCache"/>.
/// Container names must satisfy Azure Blob Storage naming rules
/// (lowercase letters, digits, hyphens; 3-63 characters).
/// </summary>
public sealed class BlobPersistentCacheOptions
{
    /// <summary>Container for game-data entries (Hot tier).</summary>
    public string GameDataContainer { get; set; } = "gamedata";

    /// <summary>Container for report metadata entries (Hot tier).</summary>
    public string MetadataContainer { get; set; } = "metadata";

    /// <summary>Container for completed-dungeon event payloads (Cool tier).</summary>
    public string EventsContainer { get; set; } = "events";

    internal string ContainerName(CachePartition partition) => partition switch
    {
        CachePartition.GameData => GameDataContainer,
        CachePartition.Metadata => MetadataContainer,
        CachePartition.Events => EventsContainer,
        _ => throw new ArgumentOutOfRangeException(nameof(partition), partition, "Unknown cache partition.")
    };
}
