using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// An aggregate stat displayed in a <see cref="CastOverview"/> section.
/// </summary>
public sealed record OverviewStat(
    string Value,
    string Label,
    string? Tooltip = null,
    QualitativePerformance? Performance = null);
