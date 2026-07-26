using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddState<Modules.Abilities>]
[AddState<ElarionAuras>]
[AddState<FocusTracker>]
[AddModule<SalvoTracker>]
[AddModule<ResurgentWindsTracker>]
[AddAnalyzer<CooldownPairingAnalyzer>]
[AddAnalyzer<FocusEconomyAnalyzer>]
[AddAnalyzer<LunarlightMarkAnalyzer>]
[AddAnalyzer<CelestialImpetusAnalyzer>]
[AddAnalyzer<ImpendingHeartseekerAnalyzer>]
[AddAnalyzer<SupremacyWindowAnalyzer>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
