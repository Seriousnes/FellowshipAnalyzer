using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Modules;

namespace FellowshipAnalyzer.Heroes.Meiko.Analysis;

[HeroAnalyzer(HeroName.Meiko)]
[AddModule<Modules.Abilities>]
public sealed partial class MeikoCombatLogParser : CombatLogParser
{
}
