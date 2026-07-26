using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Guides;
using FellowshipAnalyzer.Heroes.Rime.Modules;

namespace FellowshipAnalyzer.Heroes.Rime.Analysis;

[HeroAnalyzer(HeroName.Rime)]
[AddState<WinterOrbTracker>]
[AddAnalyzer<SingleTargetEmbraceWindowAnalyzer>]
[AddAnalyzer<AoeEmbraceWindowAnalyzer>]
[AddAnalyzer<MajorCooldownAnalyzer>]
[AddAnalyzer<DowntimeAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
