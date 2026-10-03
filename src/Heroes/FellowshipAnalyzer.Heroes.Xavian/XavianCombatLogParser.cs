using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

namespace FellowshipAnalyzer.Heroes.Xavian.Analysis;

[HeroAnalyzer(HeroName.Xavian)]
[AddAnalyzer<SwiftReprievalAnalyzer>]
[AddAnalyzer<ShiningHaloAnalyzer>]
[AddAnalyzer<SolarShieldAnalyzer>]
[AddAnalyzer<OmegaReprievalAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<XavianAuras>]
public sealed partial class XavianCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(XavianGuide);
}
