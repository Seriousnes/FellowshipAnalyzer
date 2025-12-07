using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Guides;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

[HeroAnalyzer("mara")]
[AddModule<Modules.Abilities>]
public sealed partial class MaraCombatLogParser : CombatLogParser
{
    public override string HeroId => "mara";
    public override Type? GuideComponent => typeof(MaraGuide);
}
