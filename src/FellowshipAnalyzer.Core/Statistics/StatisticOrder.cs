namespace FellowshipAnalyzer.Core.Statistics;

/// <summary>
/// Position of a statistic within its <see cref="StatisticCategory"/> section,
/// in declaration order. Ties are broken by module priority (registration order).
/// </summary>
public enum StatisticOrder
{
    Core,
    Default,
    Optional,
    Unimportant,
}
