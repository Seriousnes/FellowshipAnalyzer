using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

[HeroAnalyzer(HeroName.Mara)]
[AddModule<Modules.Abilities>]
public sealed partial class MaraCombatLogParser : CombatLogParser
{
}
