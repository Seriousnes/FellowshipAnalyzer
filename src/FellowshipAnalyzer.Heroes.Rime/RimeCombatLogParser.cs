using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Guides;
using FellowshipAnalyzer.Heroes.Rime.Modules;

namespace FellowshipAnalyzer.Heroes.Rime.Analysis;

[HeroAnalyzer("rime")]
[AddModule<WinterOrbTracker>]
[AddModule<BasicStComboAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override string HeroId => "rime";
    public override Type? GuideComponent => typeof(RimeGuide);
}


