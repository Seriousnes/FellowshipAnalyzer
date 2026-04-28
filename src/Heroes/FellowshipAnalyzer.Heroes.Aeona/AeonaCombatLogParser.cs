using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Guides;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

namespace FellowshipAnalyzer.Heroes.Aeona.Analysis;

[HeroAnalyzer(HeroName.Aeona)]
[AddModule<Modules.Abilities>]
public sealed partial class AeonaCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(AeonaGuide);
}
