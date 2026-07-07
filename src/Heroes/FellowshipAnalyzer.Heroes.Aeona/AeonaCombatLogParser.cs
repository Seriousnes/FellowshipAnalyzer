using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Aeona.Analysis;

[HeroAnalyzer(HeroName.Aeona)]
[AddModule<Modules.Abilities>]
public sealed partial class AeonaCombatLogParser : CombatLogParser
{
}
