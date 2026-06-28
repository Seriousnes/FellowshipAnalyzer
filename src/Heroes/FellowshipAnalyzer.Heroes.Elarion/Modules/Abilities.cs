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
            AbilityFacts.Multishot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            AbilityFacts.HeartseekerBarrage with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            AbilityFacts.FocusedShot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            AbilityFacts.HighwindArrow with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                CooldownReducedByHaste = true,
                Range = 30,
            },
            AbilityFacts.CelestialShot with
            {
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
                Range = 30,
            },
            AbilityFacts.StarfallVolley with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = StandardGcd,
                Range = 30,
            },
            AbilityFacts.SkystridersSupremacy with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
            },
            AbilityFacts.LunarlightMark with
            {
                Category = SpellCategory.Rotational,
                Gcd = null,
            },
            AbilityFacts.Roll with
            {
                Category = SpellCategory.Utility,
                Gcd = null,
                Cooldown = 8,
                CastableWhileCasting = true,
            },
            AbilityFacts.Disrupt with
            {
                Category = SpellCategory.Utility,
                Gcd = null,
                Range = 30,
            },
            AbilityFacts.PathfindersResillience with
            {
                Category = SpellCategory.Defensive,
                Gcd = null,
            },
            AbilityFacts.SkystridersGrace with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
                CastableWhileCasting = true,
            },
            AbilityFacts.EventHorizon with
            {
                Category = SpellCategory.Cooldowns,
                Gcd = null,
            },
            AbilityFacts.GrapplingArrow with
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
