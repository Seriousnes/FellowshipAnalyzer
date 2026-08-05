using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

[HeroAnalyzer(HeroName.Gunde)]
[AddModule<Modules.Abilities>]
[AddModule<GundeAuras>]
[AddState<BloodFeatherTracker>]
[AddState<RendStackTracker>]
[AddAnalyzer<BossSlaughterUsage>]
[AddAnalyzer<TrashSlaughterUsage>]
[AddAnalyzer<RendUptimeAnalyzer>]
[AddAnalyzer<RendSpreadAnalyzer>]
[AddAnalyzer<HeartSplitterAnalyzer>]
[AddAnalyzer<SerratedEdgeAnalyzer>]
[AddAnalyzer<BurstAnalyzer>]
[AddAnalyzer<OwedInBloodEconomyAnalyzer>]
[AddAnalyzer<CarnageAnalyzer>]
public sealed partial class GundeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(GundeGuide);
}
