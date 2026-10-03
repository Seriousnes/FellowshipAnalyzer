using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public class Abilities : CoreAbilities
{
    private const int IronSentinelHoldTheLineCharges = 2;

    /// <summary>
    /// Whether the player has Iron Sentinel equipped, which gives Hold the Line a second charge and Cooldown
    /// Acceleration equal to the player's Haste. An unowned spellbook reads the base ability.
    /// </summary>
    private bool IronSentinel => Owner?.SelectedCombatant.Legendary?.Id == Legendaries.IronSentinel.Id;

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.MeasuredStrike,
            AdditionalSpells = [Spells.MeasuredStrikeDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.PowerStrike,
            AdditionalSpells = [Spells.PowerStrikeDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.ShieldThrow,
            AdditionalSpells = [Spells.ShieldThrowDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.ShieldSlam,
            AdditionalSpells = [Spells.ShieldSlamDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Shockwave,
            AdditionalSpells = [Spells.ShockwaveDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.SweepingStrike,
            AdditionalSpells = [Spells.SweepingStrikeDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.GrandMelee,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.IronWall,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.ShieldsUp,
            Category = SpellCategory.Defensive,
            Gcd = null,
            CooldownReducedByHaste = true,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Siegebreaker,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Bash,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Charge,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = IronSentinel
                ? Spells.HoldTheLine with { Charges = IronSentinelHoldTheLineCharges }
                : Spells.HoldTheLine,
            Category = SpellCategory.Utility,
            Gcd = null,
            CooldownReducedByHaste = IronSentinel,
        },
        new()
        {
            PrimarySpell = Spells.Taunt,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            Category = SpellCategory.Hidden,
        },
    ];
}
