using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.MeasuredStrike,
            AdditionalSpells = [Spells.MeasuredStrikeDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.PowerStrike,
            AdditionalSpells = [Spells.PowerStrikeDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.ShieldThrow,
            AdditionalSpells = [Spells.ShieldThrowDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.ShieldSlam,
            AdditionalSpells = [Spells.ShieldSlamDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.Shockwave,
            AdditionalSpells = [Spells.ShockwaveDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.SweepingStrike,
            AdditionalSpells = [Spells.SweepingStrikeDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.GrandMelee,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.IronWall,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.ShieldsUp,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Siegebreaker,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Bash,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Charge,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.HoldTheLine,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Taunt,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            Category = SpellCategory.Hidden,
        },
    ];
}
