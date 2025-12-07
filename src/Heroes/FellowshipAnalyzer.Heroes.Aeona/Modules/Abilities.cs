using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() => [];
}
