using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;
using FellowshipAnalyzer.Heroes.Gunde.Normalizers;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

[HeroAnalyzer(HeroName.Gunde)]
[AddNormalizer<GundeEventLinkNormalizer>]
[AddModule<Modules.Abilities>]
[AddModule<GundeAuras>]
[AddAnalyzer<BloodFeatherTracker>]
[AddAnalyzer<RendStackTracker>]
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
