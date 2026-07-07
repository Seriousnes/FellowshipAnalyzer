using FellowshipAnalyzer.Core.Analysis;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() => [];
}
