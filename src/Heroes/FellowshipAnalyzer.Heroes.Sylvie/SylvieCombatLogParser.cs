using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

[HeroAnalyzer(HeroName.Sylvie)]
[AddModule<Modules.Abilities>]
public sealed partial class SylvieCombatLogParser : CombatLogParser
{
}
