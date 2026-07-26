using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

namespace FellowshipAnalyzer.Heroes.Tariq.Analysis;

[HeroAnalyzer(HeroName.Tariq)]
[AddState<FuryTracker>]
[AddAnalyzer<FuryEconomyAnalyzer>]
[AddAnalyzer<ThunderCallWindowAnalyzer>]
[AddAnalyzer<FocusedWrathAnalyzer>]
[AddAnalyzer<HammerStormAnalyzer>]
[AddAnalyzer<CullingStrikeAnalyzer>]
[AddModule<ExecutionersGrinTracker>]
[AddModule<RisingSpiritTracker>]
[AddModule<Modules.Abilities>]
public sealed partial class TariqCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(TariqGuide);
}
