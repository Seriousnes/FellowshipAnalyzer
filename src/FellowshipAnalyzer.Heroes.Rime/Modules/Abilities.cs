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
            Spell = RimeSpells.BrainFreeze.Id,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 20,
            Range = 30,
        },
        new()
        {
            Spell = RimeSpells.BurstingIce.Id,
            AdditionalSpellIds = [RimeSpells.BurstingIceDamage.SpellId],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 10,
            Range = 30,            
        },
        new()
        {
            Spell = RimeSpells.ColdSnap.Id,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = (Func<double, double>)(haste => 12 / (1 + haste)),
            Range = 30,
            Charges = 2,
        },
        new()
        {
            Spell = RimeSpells.FlightOfTheNavir.Id,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Cooldown = 60,
        },
        new()
        {
            Spell = RimeSpells.FreezingTorrent.Id,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Cooldown = 15,
            Range = 30,
        },
        new() 
        {
            Spell = RimeSpells.FrigidWinds.Id,
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = 60,
        },
        new()
        {
            Spell = RimeSpells.FrostBolt.Id,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            Spell = RimeSpells.FrostWard.Id,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 30,
        },
        new()
        {
            Spell = RimeSpells.GlacialBlast.Id,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            Spell = RimeSpells.IceBlitz.Id,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            Cooldown = 120,
            CastableWhileCasting = true,
        },
        new()
        {
            Spell = RimeSpells.IceComet.Id,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            Range = 30,
        },
        new()
        {
            Spell = RimeSpells.IceDash.Id,
            Category = SpellCategory.Defensive,
            Gcd = null,
            Cooldown = 25,
            Charges = 2
        },
        new()
        {
            Spell = RimeSpells.WintersBlessing.Id,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            Cooldown = 60,
        },
        new()
        {
            Spell = RimeSpells.WrathOfWinter.Id,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            Spell = RimeSpells.FrostSwallows.Id,
            AdditionalSpellIds = [RimeSpells.FrostSwallowsDamage.Id],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            Spell = Spells.VoidbringerTouch.Id,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            Spell = Spells.Kindling.SpellId,
            Category = SpellCategory.Hidden,
        },
    ];
}
