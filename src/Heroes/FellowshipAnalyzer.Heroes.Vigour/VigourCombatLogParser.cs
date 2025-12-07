using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Vigour.Guides;
using FellowshipAnalyzer.Heroes.Vigour.Modules;

namespace FellowshipAnalyzer.Heroes.Vigour.Analysis;

[HeroAnalyzer("vigour")]
[AddModule<Modules.Abilities>]
public sealed partial class VigourCombatLogParser : CombatLogParser
{
    public override string HeroId => "vigour";
    public override Type? GuideComponent => typeof(VigourGuide);
}
