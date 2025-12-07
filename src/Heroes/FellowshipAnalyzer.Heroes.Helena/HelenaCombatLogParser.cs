using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Guides;
using FellowshipAnalyzer.Heroes.Helena.Modules;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer("helena")]
[AddModule<Modules.Abilities>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
    public override string HeroId => "helena";
    public override Type? GuideComponent => typeof(HelenaGuide);
}
