using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Modules.Abilities>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
}
