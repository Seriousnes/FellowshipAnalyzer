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
            SpellDatabase.Multishot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            SpellDatabase.HeartseekerBarrage with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            SpellDatabase.FocusedShot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            SpellDatabase.HighwindArrow with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                CooldownReducedByHaste = true,
                Range = 30,
            },
            SpellDatabase.CelestialShot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            SpellDatabase.StarfallVolley with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = StandardGcd,
                Range = 30,
            },
            SpellDatabase.SkystridersSupremacy with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
            },
            SpellDatabase.LunarlightMark with
            {
                Category = SpellCategory.Rotational,
                Gcd = null,
            },
            SpellDatabase.Roll with
            {
                Category = SpellCategory.Utility,
                Gcd = null,
                Cooldown = 8,
                CastableWhileCasting = true,
            },
            SpellDatabase.Disrupt with
            {
                Category = SpellCategory.Utility,
                Gcd = null,
                Range = 30,
            },
            SpellDatabase.PathfindersResillience with
            {
                Category = SpellCategory.Defensive,
                Gcd = null,
            },
            SpellDatabase.SkystridersGrace with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
                CastableWhileCasting = true,
            },
            SpellDatabase.EventHorizon with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
            },
            SpellDatabase.GrapplingArrow with
            {
                Category = SpellCategory.Utility,
                Gcd = null,
                Cooldown = weightOfGravity ? 120 : 90,
                CooldownReducedByHaste = weightOfGravity,
                Charges = weightOfGravity ? 2 : 1,
            },
        ];
    }
}
