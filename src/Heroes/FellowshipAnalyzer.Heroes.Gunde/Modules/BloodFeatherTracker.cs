using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Gunde.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Tracks Gunde's Blood Feathers (the <see cref="ResourceTypes.Tertiary"/> resource). Feathers are
/// picked up from orbs dropped by bleeding enemies and are converted in full by Owed in Blood, so
/// the pool behaves as an accumulate-then-dump resource rather than a per-cast cost.
/// </summary>
/// <remarks>
/// The cap is fixed from the game's <c>Owed in Blood</c> constants (<c>MaxStacks</c> 150) rather
/// than trusted from event snapshots.
/// </remarks>
public class BloodFeatherTracker : ResourceTracker
{
    /// <summary>Maximum Blood Feathers Gunde can hold.</summary>
    public const int MaxBloodFeathers = 150;

    public BloodFeatherTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        MaxOverrides[ResourceTypes.Tertiary] = MaxBloodFeathers;
        DisplayNameOverrides[ResourceTypes.Tertiary] = "Blood Feathers";
    }

    /// <summary>Blood Feather state, or <c>null</c> when the resource never appeared in the log.</summary>
    public ResourceState? BloodFeathers => GetResourceState(ResourceTypes.Tertiary);

    /// <summary>
    /// The Blood Feather card, contributed only when the log actually carried the Tertiary resource.
    /// A run with no feather snapshots would otherwise render a card of zeroes.
    /// </summary>
    public override Type? StatisticsComponentType =>
        BloodFeathers is null ? null : typeof(BloodFeatherStatistics);

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    /// <summary>Total Blood Feathers gained over the fight, including feathers lost to overcap.</summary>
    public int Generated => BloodFeathers?.Generated ?? 0;

    /// <summary>Blood Feathers lost by gaining while already at the cap.</summary>
    public int Wasted => BloodFeathers?.Wasted ?? 0;

    /// <summary>Blood Feathers consumed by spenders.</summary>
    public int Spent => BloodFeathers?.Spent ?? 0;

    /// <summary>Blood Feathers held at the point last observed.</summary>
    public int Current => BloodFeathers?.Current ?? 0;
}
