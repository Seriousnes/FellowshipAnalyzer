using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Modules;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer(HeroName.Helena)]
[AddAnalyzer<ToughnessTracker>]
[AddAnalyzer<DamageTakenTracker>]
[AddAnalyzer<ToughnessAnalyzer>]
[AddAnalyzer<ShieldsUpAnalyzer>]
[AddAnalyzer<IronWallAnalyzer>]
[AddAnalyzer<LingeringConcussionAnalyzer>]
[AddAnalyzer<VeteranOfWarAnalyzer>]
[AddAnalyzer<EmpoweredShieldSlamAnalyzer>]
[AddAnalyzer<ShieldMasteryAnalyzer>]
[AddAnalyzer<SwordAndBoardAnalyzer>]
[AddAnalyzer<GreaterShockwaveAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<HelenaAuras>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(HelenaGuide);
}
