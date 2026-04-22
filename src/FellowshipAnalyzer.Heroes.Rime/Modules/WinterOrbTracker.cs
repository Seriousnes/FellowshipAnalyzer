using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class WinterOrbTracker : ResourceTracker
{
    public override Type? StatisticsComponentType => typeof(WinterOrbStatistics);

    public override void Initialize()
    {
        MaxOverrides[ResourceTypes.Secondary] = 5;
        base.Initialize();
    }

    /// <summary>Current Winter Orb count.</summary>
    public int CurrentOrbs => Secondary?.Current ?? 0;

    /// <summary>Maximum Winter Orbs Rime can hold.</summary>
    public int MaxOrbs => MaxOverrides[ResourceTypes.Secondary];

    // Convenience accessors used by WinterOrbGuide.razor and WinterOrbStatistics.razor.
    public int Generated => Secondary?.Generated ?? 0;
    public int Wasted => Secondary?.Wasted ?? 0;
    public int Spent => Secondary?.Spent ?? 0;
    public int Current => Secondary?.Current ?? 0;
}
