using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Modules;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer(HeroName.Helena)]
[AddModule<Modules.Abilities>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
}
