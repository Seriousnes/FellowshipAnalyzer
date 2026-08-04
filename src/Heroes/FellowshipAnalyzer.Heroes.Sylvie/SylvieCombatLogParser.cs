using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

[HeroAnalyzer(HeroName.Sylvie)]
[AddState<BlueyTracker>]
[AddState<PinkFlutterflyTracker>]
[AddState<SylvieManaTracker>]
[AddState<SylvieHealingEfficiencyTracker>]
[AddState<DispelTracker>]
[AddAnalyzer<OverhealAnalyzer>]
[AddAnalyzer<PinkFlutterflyAnalyzer>]
[AddAnalyzer<HeartBloomRampAnalyzer>]
[AddAnalyzer<ManaEfficiencyAnalyzer>]
[AddAnalyzer<BlueyAnalyzer>]
[AddAnalyzer<LifePetalAnalyzer>]
[AddAnalyzer<FlutterflyHealingBuffAnalyzer>]
[AddAnalyzer<AbsorbWasteAnalyzer>]
[AddAnalyzer<CureAilmentAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<SylvieAuras>]
public sealed partial class SylvieCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(SylvieGuide);
}
