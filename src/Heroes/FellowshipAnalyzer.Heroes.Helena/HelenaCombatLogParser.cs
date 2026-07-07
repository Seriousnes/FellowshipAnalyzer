using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer(HeroName.Helena)]
[AddModule<Modules.Abilities>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
}
