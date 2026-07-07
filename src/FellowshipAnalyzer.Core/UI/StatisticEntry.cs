using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// One entry on the Statistics tab. The renderer groups by <see cref="Category"/>
/// (in <see cref="StatisticCategory"/> declaration order) and within each section
/// sorts by <see cref="Order"/> (in <see cref="StatisticOrder"/> declaration order).
/// </summary>
public sealed record StatisticEntry(
    Module Module,
    Type ComponentType,
    StatisticCategory Category,
    StatisticOrder Order);
