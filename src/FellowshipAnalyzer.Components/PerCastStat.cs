namespace FellowshipAnalyzer.Components;

/// <summary>
/// A single statistic about a cast (e.g., damage dealt, targets hit).
/// </summary>
public sealed record PerCastStat(
    string Value,
    string Label,
    string? Tooltip = null,
    PerformanceTier? Performance = null);
