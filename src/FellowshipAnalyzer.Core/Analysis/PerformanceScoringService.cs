namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Default <see cref="IPerformanceScoringService"/> implementation.
/// All methods are pure, stateless computations — registered as a singleton.
/// </summary>
internal sealed class PerformanceScoringService : IPerformanceScoringService
{
    public double ComputeScore(double actual, ScoreBreakpoint[] breakpoints)
    {
        if (breakpoints.Length < 2)
            throw new ArgumentException("At least two breakpoints are required for interpolation.", nameof(breakpoints));

        if (actual <= breakpoints[0].Value)
            return Math.Clamp(breakpoints[0].Score, 0, 1);

        var last = breakpoints[^1];
        if (actual >= last.Value)
            return Math.Clamp(last.Score, 0, 1);

        for (int i = 0; i < breakpoints.Length - 1; i++)
        {
            var lower = breakpoints[i];
            var upper = breakpoints[i + 1];
            if (actual >= lower.Value && actual <= upper.Value)
            {
                double range = upper.Value - lower.Value;
                if (range == 0)
                    throw new ArgumentException(
                        $"Adjacent breakpoints at index {i} and {i + 1} have the same value ({lower.Value}).",
                        nameof(breakpoints));
                double t = (actual - lower.Value) / range;
                return Math.Clamp(lower.Score + t * (upper.Score - lower.Score), 0, 1);
            }
        }

        return Math.Clamp(last.Score, 0, 1);
    }

    public double ComputeScore(double actual, Func<double, double> strategy) =>
        Math.Clamp(strategy(actual), 0, 1);

    public double GetAggregatedScore(IReadOnlyList<ScoredCheck> checks)
    {
        if (checks.Count == 0) return 0;

        double totalWeight = 0;
        foreach (var c in checks) totalWeight += c.Weight;
        if (totalWeight == 0) return 0;

        double weightedSum = 0;
        foreach (var check in checks)
        {
            double score = Math.Clamp(check.Score, 0, 1);
            double effective = check.MinScore.HasValue
                ? Math.Max(score, Math.Clamp(check.MinScore.Value, 0, 1))
                : score;
            weightedSum += effective * check.Weight;
        }

        return Math.Clamp(weightedSum / totalWeight, 0, 1);
    }

    public PerformanceTier ScoreToTier(double score, PerformanceScoreThresholds? thresholds = null)
    {
        var t = thresholds ?? PerformanceScoreThresholds.Default;
        if (score >= t.Perfect) return PerformanceTier.Perfect;
        if (score >= t.Good)    return PerformanceTier.Good;
        if (score >= t.Ok)      return PerformanceTier.Ok;
        return PerformanceTier.Fail;
    }
}
