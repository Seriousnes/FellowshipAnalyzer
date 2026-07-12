using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.GreaterHeal,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RuneOfRenewal,
            AdditionalSpells = [Spells.RuneOfRenewalAlt],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Dawnflare,
            AdditionalSpells = [Spells.DawnflareDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Soulbrand,
            AdditionalSpells = [Spells.SoulbrandDot],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.CircleOfLight,
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RadiantBlast,
            AdditionalSpells = [Spells.RadiantBlastDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.DawnbreakerOrb,
            AdditionalSpells = [Spells.DawnbreakerOrbDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.AvatarOfLight,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RunicProliferation,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.LightshaperWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.LuminousBarrier,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.RemoveMagic,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.ThrowBook,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Levitate,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
    ];
}
