using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FireBall,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
    ];
}
