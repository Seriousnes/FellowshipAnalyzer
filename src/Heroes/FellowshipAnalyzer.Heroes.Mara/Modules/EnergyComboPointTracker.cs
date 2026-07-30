using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

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

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    public int MaxComboPointCount => MaxComboPoints;

    public ResourceState? Energy => GetResourceState(ResourceTypes.Primary);

    public ResourceState? ComboPoints => GetResourceState(ResourceTypes.Secondary);

    public int EnergyGenerated => Energy?.Generated ?? 0;

    public int EnergyWasted => Energy?.Wasted ?? 0;

    public int EnergySpent => Energy?.Spent ?? 0;

    public int EnergyCurrent => Energy?.Current ?? 0;

    public int ComboPointsGenerated => ComboPoints?.Generated ?? 0;

    public int ComboPointsWasted => ComboPoints?.Wasted ?? 0;

    public int ComboPointsCurrent => ComboPoints?.Current ?? 0;
}
