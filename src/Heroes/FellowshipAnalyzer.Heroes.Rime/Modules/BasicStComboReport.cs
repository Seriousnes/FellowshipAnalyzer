using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Analysis;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Immutable §8 projection of <see cref="BasicStComboAnalyzer"/> state. Serializable via
/// the source-generated <c>JsonSerializerContext</c>, which makes it cacheable (§7) and
/// safe to send across worker boundaries if WASM ever spawns one.
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
