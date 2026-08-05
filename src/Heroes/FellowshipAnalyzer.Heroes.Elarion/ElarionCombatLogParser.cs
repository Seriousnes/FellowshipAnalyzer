using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddModule<Modules.Abilities>]
[AddModule<ElarionAuras>]
[AddAnalyzer<FocusTracker>]
[AddAnalyzer<SalvoTracker>]
[AddAnalyzer<ResurgentWindsTracker>]
[AddAnalyzer<ImpendingHeartseekerAnalyzer>]
[AddAnalyzer<CooldownPairingAnalyzer>]
[AddAnalyzer<FocusEconomyAnalyzer>]
[AddAnalyzer<SupremacyAnalyzer>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
