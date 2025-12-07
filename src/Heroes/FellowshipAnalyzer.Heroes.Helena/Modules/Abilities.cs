using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Helena;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() => [];
}
