using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class WinterOrbTracker : ResourceTracker
{
    public override Type? StatisticsComponentType => typeof(WinterOrbStatistics);

    public override void Initialize()
    {
        //MaxOverrides[ResourceTypes.WinterOrb] = 5;
        base.Initialize();
    }

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
