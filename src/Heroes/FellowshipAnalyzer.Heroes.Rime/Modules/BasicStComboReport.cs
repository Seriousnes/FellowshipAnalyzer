using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="BasicStComboAnalyzer"/> state, produced by
/// <see cref="BasicStComboAnalyzer.OnPullEnd"/> for each pull. Serializable via the
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
    IReadOnlyList<RimeAnalyzerFinding> Findings) : IResult
{
    public BasicStComboReport() : this(
        new AnalyzerScoreCard("Bursting Ice Usage", 0, "No Bursting Ice windows detected.", "ember"),
        0, 0, 0, 0, 0L, 0, [], [])
    {
    }
}
