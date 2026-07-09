using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

[HeroAnalyzer(HeroName.Mara)]
[AddModule<Modules.Abilities>]
[AddAnalyzer<SingleTargetMaraResourceDiscipline>]
[AddAnalyzer<AoEMaraResourceDiscipline>]
public sealed partial class MaraCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(MaraGuide);
}
