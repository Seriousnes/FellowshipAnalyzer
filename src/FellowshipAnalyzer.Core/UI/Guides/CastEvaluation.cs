using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// A graded cast entry, used by <see cref="CastSummary"/> for distribution + drill-down views.
/// </summary>
public sealed record CastEvaluation(
    QualitativePerformance Performance,
    string? Reason = null,
    string? Timestamp = null);
