using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Meiko.Analysis;

[HeroAnalyzer(HeroName.Meiko)]
[AddModule<Modules.Abilities>]
public sealed partial class MeikoCombatLogParser : CombatLogParser
{
}
