using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using Items = FellowshipAnalyzer.Core.Common.Spells.Items;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.BrainFreeze,
            Category = SpellCategory.Utility,
            Gcd = null,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.BurstingIce,
            AdditionalSpells = [Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.ColdSnap,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.FlightOfTheNavir,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FreezingTorrent,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FrigidWinds,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.FrostBolt,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FrostWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.GlacialBlast,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.IceBlitz,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.IceComet,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.IceDash,
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.WintersBlessing,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.WrathOfWinter,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FrostSwallows,
            AdditionalSpells = [Spells.FrostSwallowsDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Items.VoidbringerTouch,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Spells.Kindling,
            Category = SpellCategory.Hidden,
        },
    ];
}
