using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Guides;
using FellowshipAnalyzer.Heroes.Meiko.Modules;

namespace FellowshipAnalyzer.Heroes.Meiko.Analysis;

[HeroAnalyzer("meiko")]
[AddModule<Modules.Abilities>]
public sealed partial class MeikoCombatLogParser : CombatLogParser
{
    public override string HeroId => "meiko";
    public override Type? GuideComponent => typeof(MeikoGuide);
}
