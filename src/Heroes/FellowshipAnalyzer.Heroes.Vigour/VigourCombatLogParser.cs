using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Vigour.Analysis;

[HeroAnalyzer(HeroName.Vigour)]
[AddModule<Modules.Abilities>]
public sealed partial class VigourCombatLogParser : CombatLogParser
{
}
