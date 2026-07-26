using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Mara.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// Tracks Mara's two class resources: Energy (<see cref="ResourceTypes.Primary"/>, the builder/finisher
/// currency) and Combo Points (<see cref="ResourceTypes.Secondary"/>, capped at six).
/// <para>
/// Fellowship logs never stamp an Energy <see cref="ClassResource.Cost"/> on Mara's casts, so spends are
/// resolved from the spell registry instead. Combo points have no registry cost - finishers consume
/// whatever is banked - so only Energy accumulates <see cref="ResourceState.Spent"/>.
/// </para>
/// </summary>
public sealed partial class EnergyComboPointTracker : ResourceTracker
{
    private const int MaxComboPoints = 6;

    public EnergyComboPointTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Primary] = "Energy";
        DisplayNameOverrides[ResourceTypes.Secondary] = "Combo Points";
        MaxOverrides[ResourceTypes.Secondary] = MaxComboPoints;
    }

    protected override int? GetResourceCost(CastEvent castEvent, ResourceTypes type)
    {
        var spell = SpellRegistry.MaybeGet(castEvent.Ability.FSLID);
        return type switch
        {
            ResourceTypes.Primary => spell?.Cost(ResourceTypes.Primary),
            _ => base.GetResourceCost(castEvent, type),
        };
    }

    public override Type? StatisticsComponentType => typeof(EnergyComboPointStatistics);

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    /// <summary>Mara's combo-point cap.</summary>
    public int MaxComboPointCount => MaxComboPoints;

    /// <summary>Energy state, or <c>null</c> when no event carried an Energy snapshot.</summary>
    public ResourceState? Energy => GetResourceState(ResourceTypes.Primary);

    /// <summary>Combo-point state, or <c>null</c> when no event carried a combo-point snapshot.</summary>
    public ResourceState? ComboPoints => GetResourceState(ResourceTypes.Secondary);

    /// <summary>Energy generated over the encounter, excluding Energy lost to overcap.</summary>
    public int EnergyGenerated => Energy?.Generated ?? 0;

    /// <summary>Energy lost by regenerating or gaining while already at cap.</summary>
    public int EnergyWasted => Energy?.Wasted ?? 0;

    /// <summary>Energy consumed by cast costs.</summary>
    public int EnergySpent => Energy?.Spent ?? 0;

    /// <summary>Energy held at the point last observed.</summary>
    public int EnergyCurrent => Energy?.Current ?? 0;

    /// <summary>Combo points generated over the encounter, excluding points lost to overcap.</summary>
    public int ComboPointsGenerated => ComboPoints?.Generated ?? 0;

    /// <summary>Combo points lost by generating while already at six.</summary>
    public int ComboPointsWasted => ComboPoints?.Wasted ?? 0;

    /// <summary>Combo points held at the point last observed.</summary>
    public int ComboPointsCurrent => ComboPoints?.Current ?? 0;
}
