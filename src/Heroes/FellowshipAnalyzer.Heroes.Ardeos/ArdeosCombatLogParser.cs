using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Core;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Abilities = FellowshipAnalyzer.Heroes.Ardeos.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Abilities>]
[AddModule<ArdeosDotTracker>]
[AddAnalyzer<CinderEmberTracker>]
[AddAnalyzer<RollingFlamesAnalyzer>]
[AddAnalyzer<ReignOfFireAnalyzer>]
[AddAnalyzer<RekindlingFlamesAnalyzer>]
[AddAnalyzer<WildfireComboAnalyzer>]
[AddAnalyzer<EngulfingFlamesEconomyAnalyzer>]
[AddAnalyzer<DetonateEfficiencyAnalyzer>]
[AddAnalyzer<SearingBlazeSpreadAnalyzer>]
[AddAnalyzer<SearingBlazeUptimeAnalyzer>]
[AddAnalyzer<DraconicBracersoftheDevouringFlame>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ArdeosGuide);
}
