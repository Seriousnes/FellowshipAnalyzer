using FellowshipAnalyzer.Core.Analysis;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// One entry on the Statistics tab. The renderer groups by <see cref="Category"/>
/// (in <see cref="StatisticCategory"/> declaration order) and within each section
/// sorts by <see cref="Order"/> (in <see cref="StatisticOrder"/> declaration order).
/// </summary>
/// <param name="Module">The module the card was collected from.</param>
/// <param name="Content">The module's card, taken from its <see cref="Module.Statistic"/>.</param>
/// <param name="Category">Section of the tab this card renders under.</param>
/// <param name="Order">Position within that section.</param>
public sealed record StatisticEntry(
    Module Module,
    RenderFragment Content,
    StatisticCategory Category,
    StatisticOrder Order);
