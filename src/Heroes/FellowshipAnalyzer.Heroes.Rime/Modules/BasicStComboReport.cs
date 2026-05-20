using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Immutable projection of <see cref="BasicStComboAnalyzer"/> state. Serializable via the
/// source-generated <c>JsonSerializerContext</c>, so it can be cached or sent across worker
/// boundaries.
/// </summary>
public sealed record BasicStComboReport(
    AnalyzerScoreCard ScoreCard,
    int EvaluatedWindows,
    int SuccessfulWindows,
    int PartialWindows,
    int IgnoredAoeWindows,
    long TotalBonusDamage,
    int BuffedDamageEventCount,
    IReadOnlyList<BasicStComboAnalyzer.StComboWindowEvaluation> Windows,
    IReadOnlyList<RimeAnalyzerFinding> Findings);
