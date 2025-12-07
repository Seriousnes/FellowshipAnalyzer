namespace FellowshipAnalyzer.Components;

/// <summary>
/// An aggregate stat displayed in a <see cref="CastOverview"/> section.
/// </summary>
public sealed record OverviewStat(
    string Value,
    string Label,
    string? Tooltip = null,
    PerformanceTier? Performance = null);
