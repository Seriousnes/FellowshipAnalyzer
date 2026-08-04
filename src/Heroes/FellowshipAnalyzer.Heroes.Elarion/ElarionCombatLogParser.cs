using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddState<Modules.Abilities>]
[AddState<ElarionAuras>]
[AddState<FocusTracker>]
[AddModule<SalvoTracker>]
[AddModule<ResurgentWindsTracker>]
[AddModule<ImpendingHeartseekerAnalyzer>]
[AddAnalyzer<CooldownPairingAnalyzer>]
[AddAnalyzer<FocusEconomyAnalyzer>]
[AddAnalyzer<SupremacyAnalyzer>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
