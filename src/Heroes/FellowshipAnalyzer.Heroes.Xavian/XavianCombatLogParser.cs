using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Xavian.Guides;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

namespace FellowshipAnalyzer.Heroes.Xavian.Analysis;

[HeroAnalyzer("xavian")]
[AddModule<Modules.Abilities>]
public sealed partial class XavianCombatLogParser : CombatLogParser
{
    public override string HeroId => "xavian";
    public override Type? GuideComponent => typeof(XavianGuide);
}
