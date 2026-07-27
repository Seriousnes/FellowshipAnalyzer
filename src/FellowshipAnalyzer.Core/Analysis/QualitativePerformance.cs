namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Qualitative performance rating for a cast, window, or metric.
/// </summary>
public enum QualitativePerformance
{
    /// <summary>Below the lowest scoring threshold; the check was not met.</summary>
    Fail,

    /// <summary>Meets the minimum acceptable bar but falls short of a good result.</summary>
    Ok,

    /// <summary>A solid result, above the ok threshold but short of perfect.</summary>
    Good,

    /// <summary>At or above the top scoring threshold; the check was fully met.</summary>
    Perfect,
}
