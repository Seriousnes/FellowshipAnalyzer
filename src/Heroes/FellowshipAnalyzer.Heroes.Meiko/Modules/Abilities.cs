using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public class Abilities : CoreAbilities
{
    public static GcdInfo MeikoGcd => new() { Static = 1000.0 };

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.EarthFist,
            AdditionalSpells = [Spells.EarthFistDamage],
            Category = SpellCategory.Rotational,
            Gcd = MeikoGcd,
        },
        new()
        {
            PrimarySpell = Spells.SpiritPalm,
            AdditionalSpells = [Spells.SpiritPalmDamage],
            Category = SpellCategory.Rotational,
            Gcd = MeikoGcd,
        },
        new()
        {
            PrimarySpell = Spells.WindKick,
            AdditionalSpells = [Spells.WindKickDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = MeikoGcd,
        },
        new()
        {
            PrimarySpell = Spells.RisingEarth,
            Category = SpellCategory.Rotational,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.EarthfistBarrage,
            AdditionalSpells = [Spells.EarthfistBarrageDamage, Spells.EarthfistBarrageFinalDamage],
            Category = SpellCategory.Rotational,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.RisingStorm,
            Category = SpellCategory.RotationalAoe,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.LashingStormkick,
            AdditionalSpells = [Spells.LashingStormkickDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.DoublePalmStrike,
            Category = SpellCategory.Rotational,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.SpiritedVortex,
            AdditionalSpells = [Spells.SpiritedVortexDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.ShatterEarth,
            AdditionalSpells = [Spells.ShatterEarthDamage, Spells.ShatterEarthDot],
            Category = SpellCategory.Cooldowns,
            Gcd = MeikoGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.TwinSoulsArmyOfOne,
            AdditionalSpells = [Spells.TwinSoulsArmyOfOneDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.StoneShieldAlt,
            AdditionalSpells = [Spells.StoneShield, Spells.StoneShieldDamage],
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.TwinSoulsBulwark,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Serenity,
            Category = SpellCategory.Defensive,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.StoneStomp,
            AdditionalSpells = [Spells.StoneStompDamage],
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Cyclone,
            Category = SpellCategory.Utility,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Gust,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Stagger,
            Category = SpellCategory.Utility,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Taunt,
            Category = SpellCategory.Utility,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.LashingStormkickAlt,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            AdditionalSpells = [Spells.AttackDamage],
            Category = SpellCategory.Hidden,
        },
    ];
}
