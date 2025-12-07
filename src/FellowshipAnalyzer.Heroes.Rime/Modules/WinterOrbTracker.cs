using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class WinterOrbTracker : ResourceTracker
{
    public override Type? StatisticsComponentType => typeof(WinterOrbStatistics);

    public override void Initialize()
    {
        InitialResource = 0;
        base.Initialize();
    }
}
