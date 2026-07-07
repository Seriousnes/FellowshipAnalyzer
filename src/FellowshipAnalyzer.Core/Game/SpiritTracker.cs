using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.UI.Components;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Tracks Spirit — the universal hero resource that powers Spirit of Heroism (ultimate).
/// Every hero generates and consumes Spirit, so this is a shared opt-in module: add
/// <c>[AddModule&lt;SpiritTracker&gt;]</c> to any hero parser to surface the Resources card.
/// </summary>
public sealed partial class SpiritTracker(ILogger<ResourceTracker> logger) : ResourceTracker(logger)
{
    public override Type? StatisticsComponentType => typeof(SpiritStatistics);
    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;
    public override StatisticOrder StatisticOrder => StatisticOrder.Core;
    public int Generated => Spirit?.Generated ?? 0;
    public int Spent => Spirit?.Spent ?? 0;
    public int Wasted => Spirit?.Wasted ?? 0;
    public int Current => Spirit?.Current ?? 0;
    public int Max => Spirit?.Max ?? 0;
}
