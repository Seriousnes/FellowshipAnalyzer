using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

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

    public static int EmbersFromCinders(int totalCinders) => totalCinders / CindersPerEmber;

    public static int PartialCinders(int totalCinders) => totalCinders % CindersPerEmber;

    public int TotalCinders => Primary?.Current ?? 0;

    public int CurrentEmbers => EmbersFromCinders(TotalCinders);

    public int CurrentCinders => PartialCinders(TotalCinders);
}
