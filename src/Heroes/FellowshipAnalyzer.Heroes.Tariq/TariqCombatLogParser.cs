using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

namespace FellowshipAnalyzer.Heroes.Tariq.Analysis;

[HeroAnalyzer(HeroName.Tariq)]
[AddAnalyzer<FuryTracker>]
[AddAnalyzer<FuryEconomyAnalyzer>]
[AddAnalyzer<ThunderCallAnalyzer>]
[AddAnalyzer<FocusedWrathAnalyzer>]
[AddAnalyzer<HammerStormAnalyzer>]
[AddAnalyzer<CullingStrikeAnalyzer>]
[AddAnalyzer<ExecutionersGrinTracker>]
[AddAnalyzer<RisingSpiritTracker>]
[AddModule<Modules.Abilities>]
public sealed partial class TariqCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(TariqGuide);
}
