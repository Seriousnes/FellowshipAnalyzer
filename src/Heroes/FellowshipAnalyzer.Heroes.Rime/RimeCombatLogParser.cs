using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Guides;
using FellowshipAnalyzer.Heroes.Rime.Modules;

namespace FellowshipAnalyzer.Heroes.Rime.Analysis;

[HeroAnalyzer(HeroName.Rime)]
[AddAnalyzer<WinterOrbTracker>]
[AddAnalyzer<SingleTargetEmbraceAnalyzer>]
[AddAnalyzer<AoeEmbraceAnalyzer>]
[AddAnalyzer<MajorCooldownAnalyzer>]
[AddAnalyzer<DowntimeAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
[AddAnalyzer<WintersEmbraceUpliftTracker>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
