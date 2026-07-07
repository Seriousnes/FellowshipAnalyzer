using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook()
    {
        var weightOfGravity = Owner.SelectedCombatant.HasTalent(Talents.TheWeightOfGravity.Id);
        return
        [
            new()
            {
                PrimarySpell = Spells.Multishot with { Range = 30 },
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
                PrimarySpell = Spells.FocusedShot with { Range = 30 },
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
            },
            new()
            {
                PrimarySpell = Spells.HighwindArrow with { Range = 30 },
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                CooldownReducedByHaste = true,
            },
            new()
            {
                PrimarySpell = Spells.CelestialShot with { Range = 30 },
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
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
                PrimarySpell = Spells.LunarlightMark,
                Category = SpellCategory.Rotational,
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
                PrimarySpell = Spells.PathfindersResillience,
                Category = SpellCategory.Defensive,
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
                PrimarySpell = Spells.GrapplingArrow with
                {
                    Cooldown = weightOfGravity ? 120 : 90,
                    Charges = weightOfGravity ? 2 : 1,
                },
                Category = SpellCategory.Utility,
                Gcd = null,
                CooldownReducedByHaste = weightOfGravity,
            },
        ];
    }
}
