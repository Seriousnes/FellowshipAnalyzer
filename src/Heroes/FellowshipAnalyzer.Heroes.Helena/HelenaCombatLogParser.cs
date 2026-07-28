using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Modules;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

[HeroAnalyzer(HeroName.Helena)]
[AddState<ToughnessTracker>]
[AddState<DamageTakenTracker>]
[AddAnalyzer<ToughnessBandAnalyzer>]
[AddAnalyzer<ShieldsUpAnalyzer>]
[AddAnalyzer<IronWallAnalyzer>]
[AddAnalyzer<LingeringConcussionAnalyzer>]
[AddAnalyzer<VeteranOfWarAnalyzer>]
[AddAnalyzer<EmpoweredShieldSlamAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<HelenaAuras>]
public sealed partial class HelenaCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(HelenaGuide);
}
