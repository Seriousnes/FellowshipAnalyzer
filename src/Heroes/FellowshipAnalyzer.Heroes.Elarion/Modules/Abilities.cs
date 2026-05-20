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
            PrimarySpell = Spells.Multishot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.HeartseekerBarrage,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 20,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.FocusedShot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.HighwindArrow,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Charges = 3,
            Cooldown = (Func<double, double>)(haste => 15 / (1 + haste)),
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.CelestialShot,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,            
        },
        new()
        {
            PrimarySpell = Spells.StarfallVolley,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Range = 30,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersSupremacy,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 45,
        },
        new()
        {
            PrimarySpell = Spells.LunarlightMark,
            Category = SpellCategory.Rotational,
            Gcd = null,
            Range = 30,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = Spells.Roll,
            Category = SpellCategory.Utility,
            Gcd = null,
            Charges = 3,
            Cooldown = 8,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.Disrupt,
            Category = SpellCategory.Utility,
            Gcd = null,
            Range = 30,
            Cooldown = 20,
        },
        new()
        {
            PrimarySpell = Spells.PathfindersResillience,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersGrace,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 120,
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
            PrimarySpell = Spells.GrapplingArrow,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = (Func<Combatant, double, double>)((combatant, haste) => combatant.HasTalent(Talents.TheWeightOfGravity.Id) ? 120 / (1 + haste) : 90),
            Charges = (Func<Combatant, int>)(combatant => combatant.HasTalent(Talents.TheWeightOfGravity.Id) ? 2 : 1)
        },
    ];
}
