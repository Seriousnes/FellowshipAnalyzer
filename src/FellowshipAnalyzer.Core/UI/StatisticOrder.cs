namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// Position of a statistic within its <see cref="StatisticCategory"/> section,
/// in declaration order. Ties are broken by module priority (registration order).
/// </summary>
public enum StatisticOrder
{
    /// <summary>A statistic central to the hero's rotation or build, placed first in its section.</summary>
    Core,

    /// <summary>The ordinary position for a statistic.</summary>
    Default,

    /// <summary>A statistic that is situational or only relevant for some builds.</summary>
    Optional,

    /// <summary>A statistic kept mostly for completeness, placed last in its section.</summary>
    Unimportant,
}
