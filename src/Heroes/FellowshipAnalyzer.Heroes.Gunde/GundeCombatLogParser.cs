using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

[HeroAnalyzer(HeroName.Gunde)]
[AddAnalyzer<BossSlaughterUsage>]
[AddAnalyzer<TrashSlaughterUsage>]
[AddModule<Modules.Abilities>]
public sealed partial class GundeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(GundeGuide);
}
