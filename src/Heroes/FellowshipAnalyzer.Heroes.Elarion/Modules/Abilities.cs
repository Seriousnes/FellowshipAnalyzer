using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FocusedShot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.CelestialShot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Multishot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.HighwindArrow,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.HeartseekerBarrage,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.LunarlightMark,
            Category = SpellCategory.Rotational,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.StarfallVolley,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersSupremacy,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersGrace,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.EventHorizon,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.CelestialVeil,
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Roll with { Cooldown = 8 },
            Category = SpellCategory.Utility,
            Gcd = null,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.Disrupt,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.GrapplingArrow,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
    ];
}
