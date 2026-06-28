using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        AbilityFacts.BrainFreeze with
        {
            Category = SpellCategory.Utility,
            Gcd = null,
            CastableWhileCasting = true,
        },
        AbilityFacts.BurstingIce with
        {
            AdditionalSpells = [Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        AbilityFacts.ColdSnap with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        AbilityFacts.FlightOfTheNavir with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        AbilityFacts.FreezingTorrent with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        AbilityFacts.FrigidWinds with
        {
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        AbilityFacts.FrostBolt with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        AbilityFacts.FrostWard with
        {
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        AbilityFacts.GlacialBlast with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        AbilityFacts.IceBlitz with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CastableWhileCasting = true,
        },
        AbilityFacts.IceComet with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        AbilityFacts.IceDash with
        {
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        AbilityFacts.WintersBlessing with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        AbilityFacts.WrathOfWinter with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        AbilityFacts.FrostSwallows with
        {
            AdditionalSpells = [Spells.FrostSwallowsDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Spells.VoidbringerTouch,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Spells.Kindling,
            Category = SpellCategory.Hidden,
        },
    ];
}
