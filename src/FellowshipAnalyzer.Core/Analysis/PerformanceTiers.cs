namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Maps a value onto a <see cref="QualitativePerformance"/> by descending thresholds.</summary>
public static class PerformanceTiers
{
    /// <summary>
    /// <see cref="QualitativePerformance.Perfect"/> at or above <paramref name="perfect"/>, then
    /// <see cref="QualitativePerformance.Good"/> at <paramref name="good"/>,
    /// <see cref="QualitativePerformance.Ok"/> at <paramref name="ok"/>, otherwise
    /// <see cref="QualitativePerformance.Fail"/>. Thresholds must be supplied in descending order.
    /// </summary>
    public static QualitativePerformance FromThresholds(double value, double perfect, double good, double ok) =>
        value >= perfect ? QualitativePerformance.Perfect
        : value >= good ? QualitativePerformance.Good
        : value >= ok ? QualitativePerformance.Ok
        : QualitativePerformance.Fail;
}
