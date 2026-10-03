using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Modules;

namespace FellowshipAnalyzer.Heroes.Meiko.Analysis;

[HeroAnalyzer(HeroName.Meiko)]
[AddAnalyzer<GuardiansFluidityAnalyzer>]
[AddAnalyzer<EarthbourneAnalyzer>]
[AddAnalyzer<StoneShieldAnalyzer>]
[AddAnalyzer<SpiritedStrikesAnalyzer>]
[AddAnalyzer<SpiritedVortexAnalyzer>]
[AddAnalyzer<EarthfallAnalyzer>]
[AddModule<Modules.Abilities>]
public sealed partial class MeikoCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(MeikoGuide);
}
