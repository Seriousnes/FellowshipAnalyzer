using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        // Core
        new()
        {
            PrimarySpell = RimeSpells.BrainFreeze,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 20,
            Range = 30,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = RimeSpells.BurstingIce,
            AdditionalSpells = [RimeSpells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 10,
            Range = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.ColdSnap,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = (Func<double, double>)(haste => 12 / (1 + haste)),
            Range = 30,
            Charges = 2,
        },
        new()
        {
            PrimarySpell = RimeSpells.FlightOfTheNavir,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = RimeSpells.FreezingTorrent,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 15,
            Range = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.FrigidWinds,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = RimeSpells.FrostBolt,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.FrostWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.GlacialBlast,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.IceBlitz,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 120,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = RimeSpells.IceComet,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = RimeSpells.IceDash,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 25,
            Charges = 2
        },
        new()
        {
            PrimarySpell = RimeSpells.WintersBlessing,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = RimeSpells.WrathOfWinter,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = RimeSpells.FrostSwallows,
            AdditionalSpells = [RimeSpells.FrostSwallowsDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.VoidbringerTouch,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Spells.Kindling,
            Category = SpellCategory.Hidden,
        },
    ];
}
