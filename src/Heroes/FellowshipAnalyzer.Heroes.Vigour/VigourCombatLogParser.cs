using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Vigour.Modules;

namespace FellowshipAnalyzer.Heroes.Vigour.Analysis;

[HeroAnalyzer(HeroName.Vigour)]
[AddModule<Modules.Abilities>]
[AddAnalyzer<RuneOfRenewalAnalyzer>]
[AddAnalyzer<RunicProliferationAnalyzer>]
[AddAnalyzer<DawnbreakerOrbAnalyzer>]
[AddAnalyzer<RadiantBlastAnalyzer>]
[AddAnalyzer<SoulbrandAnalyzer>]
[AddAnalyzer<VigourSpellUsable>]
[AddAnalyzer<RemoveMagicAnalyzer>]
[AddAnalyzer<SacredBarrierAnalyzer>]
public sealed partial class VigourCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(VigourGuide);
}
