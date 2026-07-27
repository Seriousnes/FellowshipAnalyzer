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
            PrimarySpell = Spells.FocusedShot with { Range = 30 },
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.CelestialShot with { Range = 30 },
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Multishot with { Range = 30 },
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.HighwindArrow with { Range = 30 },
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.HeartseekerBarrage with { Range = 30 },
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
            PrimarySpell = Spells.StarfallVolley with { Range = 30 },
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
            PrimarySpell = Spells.Disrupt with { Range = 30 },
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
