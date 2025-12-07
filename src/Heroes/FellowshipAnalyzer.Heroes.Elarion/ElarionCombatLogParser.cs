using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Guides;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

[HeroAnalyzer("elarion")]
[AddModule<Modules.Abilities>]
public sealed partial class ElarionCombatLogParser : CombatLogParser
{
    public override string HeroId => "elarion";
    public override Type? GuideComponent => typeof(ElarionGuide);
}
