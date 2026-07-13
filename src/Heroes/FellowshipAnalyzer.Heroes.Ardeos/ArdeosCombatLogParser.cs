using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Abilities = FellowshipAnalyzer.Heroes.Ardeos.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Abilities>]
[AddModule<CinderEmberTracker>]
[AddModule<RollingFlamesAnalyzer>]
[AddModule<ReignOfFireAnalyzer>]
[AddAnalyzer<WildfireComboAnalyzer>]
[AddAnalyzer<EngulfingFlamesEconomyAnalyzer>]
[AddAnalyzer<SearingBlazeSpreadAnalyzer>]
[AddAnalyzer<SearingBlazeUptimeAnalyzer>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ArdeosGuide);
}
