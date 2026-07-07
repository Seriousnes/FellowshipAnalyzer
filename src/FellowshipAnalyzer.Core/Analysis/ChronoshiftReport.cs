namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Projection of <see cref="ChronoshiftAnalyzer"/> state — aggregated CDR per spell across all
/// observed Chronoshift channel windows. Returned by <see cref="ChronoshiftAnalyzer.ToReport"/>.
/// </summary>
public sealed record ChronoshiftReport(
    IReadOnlyDictionary<int, int> TotalAppliedBySpell,
    IReadOnlyDictionary<int, int> TotalWastedBySpell);
