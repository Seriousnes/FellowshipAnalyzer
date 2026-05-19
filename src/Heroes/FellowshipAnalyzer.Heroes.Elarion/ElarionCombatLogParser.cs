using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Guides;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddModule<Modules.Abilities>]
[AddModule<ElarionAuras>]
[AddModule<FocusTracker>]
[AddModule<EmpoweredMultishotWasteAnalyzer>]
[AddModule<HighwindArrowCapAnalyzer>]
[AddModule<LunarlightMarkEruptionAnalyzer>]
[AddModule<CooldownEfficiencyAnalyzer>]
[AddModule<ImpendingHeartseekerAnalyzer>]
[AddModule<StarfallVolleyDesyncAnalyzer>]
[AddModule<VoidbringerTouchAnalyzer>]
[AddModule<PreUltimateChecklistAnalyzer>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
