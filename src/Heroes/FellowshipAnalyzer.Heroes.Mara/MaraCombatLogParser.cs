using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

[HeroAnalyzer(HeroName.Mara)]
[AddState<EnergyComboPointTracker>]
[AddModule<Modules.Abilities>]
[AddModule<MaraAuras>]
[AddAnalyzer<SingleTargetMaraResourceDiscipline>]
[AddAnalyzer<AoEMaraResourceDiscipline>]
[AddAnalyzer<MaraDotUptimeAnalyzer>]
[AddAnalyzer<MaraDotSpreadAnalyzer>]
[AddAnalyzer<MaidenOfDeathWindowAnalyzer>]
public sealed partial class MaraCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(MaraGuide);
}
