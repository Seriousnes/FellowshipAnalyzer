namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Maps a value onto a <see cref="PerformanceTier"/> by descending thresholds.</summary>
public static class PerformanceTiers
{
    /// <summary>
    /// <see cref="PerformanceTier.Perfect"/> at or above <paramref name="perfect"/>, then
    /// <see cref="PerformanceTier.Good"/> at <paramref name="good"/>,
    /// <see cref="PerformanceTier.Ok"/> at <paramref name="ok"/>, otherwise
    /// <see cref="PerformanceTier.Fail"/>. Thresholds must be supplied in descending order.
    /// </summary>
    public static PerformanceTier FromThresholds(double value, double perfect, double good, double ok) =>
        value >= perfect ? PerformanceTier.Perfect
        : value >= good ? PerformanceTier.Good
        : value >= ok ? PerformanceTier.Ok
        : PerformanceTier.Fail;
}
