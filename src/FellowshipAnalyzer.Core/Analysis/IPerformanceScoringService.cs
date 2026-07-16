namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A breakpoint for linear-interpolation scoring.
/// Breakpoints must be sorted ascending by <see cref="Value"/>.
/// </summary>
/// <param name="Value">The raw input value at this breakpoint.</param>
/// <param name="Score">The score (0.0–1.0) at this breakpoint.</param>
public record ScoreBreakpoint(double Value, double Score);

/// <summary>
/// A single scored check with a relative importance weighting and optional score floor.
/// </summary>
/// <param name="Score">The raw score in [0.0, 1.0].</param>
/// <param name="Weight">Relative importance — only ratios between weights matter.</param>
/// <param name="MinScore">
/// Optional floor [0.0, 1.0]: even a score of 0 contributes at least this much.
/// Useful for secondary checks that should influence but not dominate the overall result.
/// </param>
public record ScoredCheck(double Score, double Weight, double? MinScore = null);

/// <summary>
/// Configurable thresholds for mapping a 0.0–1.0 score to a <see cref="QualitativePerformance"/>.
/// </summary>
public record PerformanceScoreThresholds(
    double Perfect = 0.95,
    double Good = 0.75,
    double Ok = 0.5)
{
    /// <summary>Default thresholds: perfect ≥ 0.95, good ≥ 0.75, ok ≥ 0.5.</summary>
    public static PerformanceScoreThresholds Default { get; } = new();
}

/// <summary>
/// Provides weighted scoring and tier-mapping utilities for analyzer modules.
/// This is the primary aggregate scoring mechanism — prefer this over raw boolean pass/fail checks.
/// Register via <c>services.AddCoreAnalysisServices()</c> and inject into any analyzer module.
/// </summary>
public interface IPerformanceScoringService
{
    /// <summary>
    /// Maps <paramref name="actual"/> to a score in [0.0, 1.0] via linear interpolation
    /// between the provided <paramref name="breakpoints"/>.
    /// Breakpoints must be sorted ascending by <see cref="ScoreBreakpoint.Value"/>.
    /// Values outside the range are clamped to the nearest boundary's score.
    /// </summary>
    double ComputeScore(double actual, ScoreBreakpoint[] breakpoints);

    /// <summary>
    /// Maps <paramref name="actual"/> to a score in [0.0, 1.0] via a custom scoring function.
    /// The result is clamped to [0, 1].
    /// </summary>
    double ComputeScore(double actual, Func<double, double> strategy);

    /// <summary>
    /// Computes a weighted average score from an array of checks, applying per-check
    /// score floors (<see cref="ScoredCheck.MinScore"/>) when specified.
    /// Returns 0 for an empty list or when total weight is zero.
    /// </summary>
    double GetAggregatedScore(IReadOnlyList<ScoredCheck> checks);

    /// <summary>
    /// Maps a 0.0–1.0 score to a <see cref="QualitativePerformance"/> using configurable thresholds.
    /// Falls back to <see cref="PerformanceScoreThresholds.Default"/> when not specified.
    /// </summary>
    QualitativePerformance ScoreToTier(double score, PerformanceScoreThresholds? thresholds = null);
}
