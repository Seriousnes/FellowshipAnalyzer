namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>Sizing variant controlling a statistics card's vertical footprint, used by <c>Statistic</c> and <c>StatCard</c>.</summary>
public enum StatisticSize
{
    /// <summary>The most compact card footprint.</summary>
    Small,
    /// <summary>The default card footprint.</summary>
    Standard,
    /// <summary>A footprint between <see cref="Standard"/> and <see cref="Large"/>.</summary>
    Medium,
    /// <summary>The tallest fixed card footprint.</summary>
    Large,
    /// <summary>A footprint that grows to fit its content rather than using a fixed height.</summary>
    Flexible,
}
