using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

[HeroAnalyzer(HeroName.Mara)]
[AddState<EnergyComboPointTracker>]
[AddModule<Modules.Abilities>]
[AddModule<MaraAuras>]
[AddModule<DeadlySchemeTracker>]
[AddAnalyzer<FinalStratagemAnalyzer>]
[AddAnalyzer<SingleTargetMaraResourceDiscipline>]
[AddAnalyzer<AoEMaraResourceDiscipline>]
[AddAnalyzer<MaraDotUptimeAnalyzer>]
[AddAnalyzer<MaraDotSpreadAnalyzer>]
[AddAnalyzer<MaidenOfDeathAnalyzer>]
[AddAnalyzer<MatriarchMacabreAnalyzer>]
[AddAnalyzer<StealthAnalyzer>]
[AddAnalyzer<GuileAnalyzer>]
public sealed partial class MaraCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(MaraGuide);
}
