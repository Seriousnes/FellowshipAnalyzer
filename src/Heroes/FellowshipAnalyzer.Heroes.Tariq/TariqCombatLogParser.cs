using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Tariq.Analysis;

[HeroAnalyzer(HeroName.Tariq)]
[AddModule<Modules.Abilities>]
public sealed partial class TariqCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(TariqGuide);
}
