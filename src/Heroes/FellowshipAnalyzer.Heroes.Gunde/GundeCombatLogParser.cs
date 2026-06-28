using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

[HeroAnalyzer(HeroName.Gunde)]
[AddModule<Modules.Abilities>]
public sealed partial class GundeCombatLogParser : CombatLogParser
{
}
