using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Modules.Abilities>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
}
