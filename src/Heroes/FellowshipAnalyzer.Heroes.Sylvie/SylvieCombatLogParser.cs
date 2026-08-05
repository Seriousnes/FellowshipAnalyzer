using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

[HeroAnalyzer(HeroName.Sylvie)]
[AddState<BlueyTracker>]
[AddState<PinkFlutterflyTracker>]
[AddState<SylvieManaTracker>]
[AddState<SylvieHealingEfficiencyTracker>]
[AddState<DispelTracker>]
[AddAnalyzer<PinkFlutterflyAnalyzer>]
[AddAnalyzer<HeartBloomRampAnalyzer>]
[AddAnalyzer<BlueyAnalyzer>]
[AddAnalyzer<LifePetalAnalyzer>]
[AddAnalyzer<PricklyVineAnalyzer>]
[AddAnalyzer<SafeHavenAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<SylvieAuras>]
public sealed partial class SylvieCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(SylvieGuide);
}
