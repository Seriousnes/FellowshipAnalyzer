using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Guides;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

[HeroAnalyzer("ardeos")]
[AddModule<Modules.Abilities>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
    public override string HeroId => "ardeos";
    public override Type? GuideComponent => typeof(ArdeosGuide);
}
