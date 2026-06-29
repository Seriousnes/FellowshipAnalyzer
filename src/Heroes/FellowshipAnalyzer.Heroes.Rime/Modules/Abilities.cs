using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        SpellDatabase.BrainFreeze with
        {
            Category = SpellCategory.Utility,
            Gcd = null,
            CastableWhileCasting = true,
        },
        SpellDatabase.BurstingIce with
        {
            AdditionalSpells = [Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        SpellDatabase.ColdSnap with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        SpellDatabase.FlightOfTheNavir with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        SpellDatabase.FreezingTorrent with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        SpellDatabase.FrigidWinds with
        {
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        SpellDatabase.FrostBolt with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        SpellDatabase.FrostWard with
        {
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        SpellDatabase.GlacialBlast with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        SpellDatabase.IceBlitz with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CastableWhileCasting = true,
        },
        SpellDatabase.IceComet with
        {
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        SpellDatabase.IceDash with
        {
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        SpellDatabase.WintersBlessing with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        SpellDatabase.WrathOfWinter with
        {
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        SpellDatabase.FrostSwallows with
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
