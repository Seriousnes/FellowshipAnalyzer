using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Helena's spellbook. <c>CooldownReducedByHaste</c> follows the Season 3 <c>Kit</c> block's
/// <c>SpellCDHasted</c> flag, which is set for Shockwave, Power Strike and Shield Throw alone. The flag
/// is known to under-report rather than over-report, so an ability found to recharge faster than its
/// configured cooldown allows may need it despite the source saying otherwise.
/// </summary>
public class Abilities : CoreAbilities
{
    /// <inheritdoc/>
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
            PrimarySpell = Spells.HoldTheLine,
            Category = SpellCategory.Utility,
            Gcd = null,
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
