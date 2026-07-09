using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Heroes.Ardeos.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Tracks Ardeos's Cinders as two derived resources decomposed from the logged
/// <see cref="ResourceTypes.Primary"/> total: whole burning Embers and the Cinder progress
/// toward the next Ember. After <c>ResourceNormalizer</c> scaling, the Primary value is the
/// in-game Cinder total (0-400), where every 100 Cinders form one of the four Embers.
/// </summary>
public sealed partial class CinderEmberTracker : ResourceTracker
{
    public const int CindersPerEmber = 100;
    public const int MaxEmbers = 4;
    public const int MaxCinders = MaxEmbers * CindersPerEmber;

    public CinderEmberTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        MaxOverrides[ResourceTypes.Primary] = MaxCinders;
        DisplayNameOverrides[ResourceTypes.Primary] = "Cinders";

        Active = true;
    }

    public override Type? StatisticsComponentType => typeof(CinderEmberStatistics);

    /// <summary>Whole burning Embers held for a given Cinder total.</summary>
    public static int EmbersFromCinders(int totalCinders) => totalCinders / CindersPerEmber;

    /// <summary>Cinder progress (0-99) toward the next Ember for a given Cinder total.</summary>
    public static int PartialCinders(int totalCinders) => totalCinders % CindersPerEmber;

    /// <summary>The most recently observed Cinder total (0-400).</summary>
    public int TotalCinders => Primary?.Current ?? 0;

    /// <summary>Whole burning Embers currently held (0-4).</summary>
    public int CurrentEmbers => EmbersFromCinders(TotalCinders);

    /// <summary>Cinder progress (0-99) toward the next Ember.</summary>
    public int CurrentCinders => PartialCinders(TotalCinders);
}
