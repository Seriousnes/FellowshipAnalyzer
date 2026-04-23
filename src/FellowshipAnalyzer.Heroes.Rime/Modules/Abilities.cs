using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
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
            PrimarySpell = Core.Common.Spells.Rime.Spells.BrainFreeze,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 20,
            Range = 30,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.BurstingIce,
            AdditionalSpells = [Core.Common.Spells.Rime.Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 10,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.ColdSnap,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = (Func<double, double>)(haste => 12 / (1 + haste)),
            Range = 30,
            Charges = 2,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FlightOfTheNavir,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FreezingTorrent,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 15,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FrigidWinds,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FrostBolt,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FrostWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.GlacialBlast,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.IceBlitz,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 120,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.IceComet,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.IceDash,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 25,
            Charges = 2
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.WintersBlessing,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.WrathOfWinter,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Rime.Spells.FrostSwallows,
            AdditionalSpells = [Core.Common.Spells.Rime.Spells.FrostSwallowsDamage],
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
