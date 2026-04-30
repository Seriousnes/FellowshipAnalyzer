using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Xavian.Analysis;

[HeroAnalyzer(HeroName.Xavian)]
[AddModule<Modules.Abilities>]
public sealed partial class XavianCombatLogParser : CombatLogParser
{
}
