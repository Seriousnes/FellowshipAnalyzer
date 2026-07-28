using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

[HeroAnalyzer(HeroName.Sylvie)]
[AddState<BlueyTracker>]
[AddState<PinkButterflyTracker>]
[AddState<SylvieManaTracker>]
[AddState<SylvieHealingEfficiencyTracker>]
[AddState<DispelTracker>]
[AddAnalyzer<OverhealAnalyzer>]
[AddAnalyzer<PinkButterflyAssignmentAnalyzer>]
[AddAnalyzer<HeartBloomRampAnalyzer>]
[AddAnalyzer<ManaEfficiencyAnalyzer>]
[AddAnalyzer<BlueyAssignmentAnalyzer>]
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
