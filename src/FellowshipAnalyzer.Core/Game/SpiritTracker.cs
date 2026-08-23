using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Tracks Spirit - the universal hero resource that powers Spirit of Heroism (ultimate).
/// Every hero generates and consumes Spirit, so this is a shared opt-in module: add
/// <c>[AddModule&lt;SpiritTracker&gt;]</c> to any hero parser to surface the Resources card.
/// </summary>
public sealed partial class SpiritTracker(ILogger<ResourceTracker> logger) : ResourceTracker(logger)
{
    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;
    /// <inheritdoc/>
    public override StatisticOrder StatisticOrder => StatisticOrder.Core;
    /// <summary>Total Spirit gained by the player across the parse, or zero if Spirit has not yet been observed.</summary>
    public int Generated => Spirit?.Generated ?? 0;
    /// <summary>Total Spirit spent by the player, typically on Spirit of Heroism casts, across the parse.</summary>
    public int Spent => Spirit?.Spent ?? 0;
    /// <summary>Total Spirit generated while already at or above the cap, and therefore lost.</summary>
    public int Wasted => Spirit?.Wasted ?? 0;
    /// <summary>The player's most recently observed Spirit amount.</summary>
    public int Current => Spirit?.Current ?? 0;
    /// <summary>The player's most recently observed maximum Spirit capacity.</summary>
    public int Max => Spirit?.Max ?? 0;
}
