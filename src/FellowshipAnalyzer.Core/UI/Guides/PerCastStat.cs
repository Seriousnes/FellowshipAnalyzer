using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// A single statistic about a cast (e.g., damage dealt, targets hit).
/// </summary>
public sealed record PerCastStat(
    string Value,
    string Label,
    string? Tooltip = null,
    QualitativePerformance? Performance = null);
