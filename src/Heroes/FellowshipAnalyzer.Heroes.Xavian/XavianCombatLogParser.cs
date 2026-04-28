using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Xavian.Guides;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

namespace FellowshipAnalyzer.Heroes.Xavian.Analysis;

[HeroAnalyzer(HeroName.Xavian)]
[AddModule<Modules.Abilities>]
public sealed partial class XavianCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(XavianGuide);
}
