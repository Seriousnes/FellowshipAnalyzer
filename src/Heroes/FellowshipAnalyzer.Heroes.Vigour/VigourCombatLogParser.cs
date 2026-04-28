using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Vigour.Guides;
using FellowshipAnalyzer.Heroes.Vigour.Modules;

namespace FellowshipAnalyzer.Heroes.Vigour.Analysis;

[HeroAnalyzer(HeroName.Vigour)]
[AddModule<Modules.Abilities>]
public sealed partial class VigourCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(VigourGuide);
}
