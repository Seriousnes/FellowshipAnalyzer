namespace FellowshipAnalyzer.Components;

/// <summary>
/// An entry in a <see cref="PerformanceBoxRow"/>.
/// </summary>
public sealed record PerformanceBoxEntry(
    PerformanceTier Performance,
    string? Tooltip = null);
