using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Guides;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddState<Modules.Abilities>]
[AddState<ElarionAuras>]
[AddState<FocusTracker>]
[AddAnalyzer<EmpoweredMultishotWasteAnalyzer>]
[AddAnalyzer<HighwindArrowCapAnalyzer>]
[AddAnalyzer<LunarlightMarkEruptionAnalyzer>]
[AddAnalyzer<CooldownEfficiencyAnalyzer>]
[AddAnalyzer<ImpendingHeartseekerAnalyzer>]
[AddAnalyzer<StarfallVolleyDesyncAnalyzer>]
[AddAnalyzer<VoidbringerTouchAnalyzer>]
[AddAnalyzer<PreUltimateChecklistAnalyzer>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
