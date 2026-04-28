using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Guides;
using FellowshipAnalyzer.Heroes.Sylvie.Modules;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

[HeroAnalyzer(HeroName.Sylvie)]
[AddModule<Modules.Abilities>]
public sealed partial class SylvieCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(SylvieGuide);
}
