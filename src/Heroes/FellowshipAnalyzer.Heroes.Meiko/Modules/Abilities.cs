using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.EarthFist,
            AdditionalSpells = [Spells.EarthFistDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.SpiritPalm,
            AdditionalSpells = [Spells.SpiritPalmDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.WindKick,
            AdditionalSpells = [Spells.WindKickDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.RisingEarth,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.EarthfistBarrage,
            AdditionalSpells = [Spells.EarthfistBarrageDamage, Spells.EarthfistBarrageFinalDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.RisingStorm,
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.LashingStormkick,
            AdditionalSpells = [Spells.LashingStormkickDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.DoublePalmStrike,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.SpiritedVortex,
            AdditionalSpells = [Spells.SpiritedVortexDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.ShatterEarth,
            AdditionalSpells = [Spells.ShatterEarthDamage, Spells.ShatterEarthDot],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.TwinSoulsArmyOfOne,
            AdditionalSpells = [Spells.TwinSoulsArmyOfOneDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.StoneShieldAlt,
            AdditionalSpells = [Spells.StoneShield, Spells.StoneShieldDamage],
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.TwinSoulsBulwark,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Serenity,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.StoneStomp,
            AdditionalSpells = [Spells.StoneStompDamage],
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Cyclone,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Gust,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Stagger,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Taunt,
            Category = SpellCategory.Utility,
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
