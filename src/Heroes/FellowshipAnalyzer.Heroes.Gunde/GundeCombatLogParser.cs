using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

[HeroAnalyzer(HeroName.Gunde)]
[AddState<BloodFeatherTracker>]
[AddAnalyzer<BossSlaughterUsage>]
[AddAnalyzer<TrashSlaughterUsage>]
[AddAnalyzer<RendUptimeAnalyzer>]
[AddAnalyzer<RendSpreadAnalyzer>]
[AddAnalyzer<BurstWindowAnalyzer>]
[AddAnalyzer<OwedInBloodEconomyAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<GundeAuras>]
[AddModule<FatedStrikeWindowTracker>]
public sealed partial class GundeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(GundeGuide);
}
