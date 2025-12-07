using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Guides;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

namespace FellowshipAnalyzer.Heroes.Tariq.Analysis;

[HeroAnalyzer("tariq")]
[AddModule<Modules.Abilities>]
public sealed partial class TariqCombatLogParser : CombatLogParser
{
    public override string HeroId => "tariq";
    public override Type? GuideComponent => typeof(TariqGuide);
}
