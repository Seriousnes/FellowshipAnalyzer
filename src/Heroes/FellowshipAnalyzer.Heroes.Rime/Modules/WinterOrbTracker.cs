using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed partial class WinterOrbTracker(ILogger<ResourceTracker> logger) : ResourceTracker(logger)
{
    protected override int? GetResourceCost(CastEvent e, ResourceTypes type)
    {
        var spell = SpellRegistry.MaybeGet(e.Ability.Guid) as IRimeSpell;
        return type switch
        {
            ResourceTypes.WinterOrb => spell?.WinterOrbCost,
            ResourceTypes.Primary => spell?.AnimaCost,
            _ => base.GetResourceCost(e, type),
        };
    }

    public override Type? StatisticsComponentType => typeof(WinterOrbStatistics);

    public ResourceState? WinterOrbs => GetResourceState(ResourceTypes.WinterOrb);

    /// <summary>Current Winter Orb count.</summary>
    public int CurrentOrbs => WinterOrbs?.Current ?? 0;

    /// <summary>Maximum Winter Orbs Rime can hold.</summary>
    public int MaxOrbs => MaxOverrides[ResourceTypes.WinterOrb];

    // Convenience accessors used by WinterOrbGuide.razor and WinterOrbStatistics.razor.
    public int Generated => WinterOrbs?.Generated ?? 0;
    public int Wasted => WinterOrbs?.Wasted ?? 0;
    public int Spent => WinterOrbs?.Spent ?? 0;
    public int Current => WinterOrbs?.Current ?? 0;
}
