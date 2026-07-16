using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// An entry in a <see cref="PerformanceBoxRow"/>.
/// </summary>
public sealed record PerformanceBoxEntry(
    QualitativePerformance Performance,
    string? Tooltip = null);
