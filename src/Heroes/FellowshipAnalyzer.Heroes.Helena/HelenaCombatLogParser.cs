using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Guides;
using FellowshipAnalyzer.Heroes.Helena.Modules;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer(HeroName.Helena)]
[AddModule<Modules.Abilities>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(HelenaGuide);
}
