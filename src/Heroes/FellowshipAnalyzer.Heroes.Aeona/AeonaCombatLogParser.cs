using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Aeona.Analysis;

[HeroAnalyzer(HeroName.Aeona)]
[AddModule<Modules.Abilities>]
[AddModule<Modules.AeonaBuild>]
[AddAnalyzer<Modules.AeonaStatBuffs>]
[AddAnalyzer<Modules.ConvergingTimelinesTracker>]
[AddAnalyzer<Modules.ChronaTracker>]
[AddAnalyzer<Modules.StaggerTracker>]
[AddAnalyzer<Modules.UchroniaTracker>]
[AddAnalyzer<Modules.FreeCastTracker>]
[AddAnalyzer<Modules.CrossAbilityAnalyzer>]
[AddAnalyzer<Modules.SynchronicityAnalyzer>]
[AddAnalyzer<Modules.StaggerCleanseAnalyzer>]
[AddAnalyzer<Modules.OblivionAnalyzer>]
[AddAnalyzer<Modules.TimeShardAnalyzer>]
[AddAnalyzer<Modules.ContinuumShiftAnalyzer>]
[AddAnalyzer<Modules.KindRewindAnalyzer>]
[AddAnalyzer<Modules.TwilightSkyboltAnalyzer>]
[AddAnalyzer<Modules.UnfoldingDoomAnalyzer>]
[AddAnalyzer<Modules.EntropyClaimAnalyzer>]
[AddAnalyzer<Modules.ChronaTapAnalyzer>]
[AddAnalyzer<Modules.ResonantFateAnalyzer>]
[AddAnalyzer<Modules.FleetingHourAnalyzer>]
[AddAnalyzer<Modules.TemporalBarrageAnalyzer>]
public sealed partial class AeonaCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(AeonaGuide);
}
