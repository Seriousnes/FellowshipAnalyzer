using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer(HeroName.Elarion)]
[AddState<Modules.Abilities>]
[AddState<ElarionAuras>]
[AddState<FocusTracker>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ElarionGuide);
}
