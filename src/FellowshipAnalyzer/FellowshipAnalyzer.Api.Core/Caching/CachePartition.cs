namespace FellowshipAnalyzer.Api.Core.Caching;

/// <summary>
/// Identifies a named partition in the persistent cache.
/// Each partition maps to a separate blob container in Azure Blob Storage.
/// </summary>
public enum CachePartition
{
    /// <summary>
    /// Game data (abilities, talent trees, etc.) that changes only on patch.
    /// Maps to the <c>gamedata</c> container (Hot tier).
    /// </summary>
    GameData,

    /// <summary>
    /// Report metadata - <see cref="FellowshipAnalyzer.Core.FellowshipLogs.AnalysisPreload"/>
    /// and <see cref="FellowshipAnalyzer.Core.FellowshipLogs.CharacterReports"/>.
    /// Maps to the <c>metadata</c> container (Hot tier).
    /// </summary>
    Metadata,

    /// <summary>
    /// Raw combat event JSON for completed dungeons (stored gzip-compressed).
    /// Maps to the <c>events</c> container (Cool tier).
    /// </summary>
    Events,

    /// <summary>
    /// Model insights for one guide, keyed by a hash of the model, effort, system prompt and user message.
    /// Maps to the <c>insights</c> container (Hot tier).
    /// </summary>
    Insights,
}
