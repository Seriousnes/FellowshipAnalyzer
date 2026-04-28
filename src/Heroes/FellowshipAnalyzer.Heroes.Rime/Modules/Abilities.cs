using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        // Core
        new()
        {
            PrimarySpell = Spells.BrainFreeze,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 20,
            Range = 30,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.BurstingIce,
            AdditionalSpells = [Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 10,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.ColdSnap,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = (Func<double, double>)(haste => 12 / (1 + haste)),
            Range = 30,
            Charges = 2,
        },
        new()
        {
            PrimarySpell = Spells.FlightOfTheNavir,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = Spells.FreezingTorrent,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 15,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.FrigidWinds,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = Spells.FrostBolt,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.FrostWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = Spells.GlacialBlast,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.IceBlitz,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 120,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.IceComet,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Spells.IceDash,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 25,
            Charges = 2
        },
        new()
        {
            PrimarySpell = Spells.WintersBlessing,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 60,
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
