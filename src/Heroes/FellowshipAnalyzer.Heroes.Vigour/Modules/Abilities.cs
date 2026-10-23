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
            PrimarySpell = Spells.Dawnflare,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Soulbrand,
            AdditionalSpells = [Spells.SoulbrandDot, Spells.SoulbrandBuffActiveAdditionalDirectDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RuneOfRenewal,
            AdditionalSpells = [Spells.RuneOfRenewalBuff, Spells.RuneOfRenewalEcho],
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.LuminousBarrier,
            AdditionalSpells = [Spells.LuminousBarrierAbsorb],
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RadiantBlast,
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.DawnbreakerOrb,
            AdditionalSpells = [Spells.MeticulousRunesmithAbsorb],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.CircleOfLight,
            AdditionalSpells = [Spells.CeremonyOfLightBuff],
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.GreaterHeal,
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.AvatarOfLight,
            AdditionalSpells = [Spells.AvatarOfLightBuff],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RunicProliferation,
            AdditionalSpells = [Spells.RunicProliferationBuff],
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.LightshaperWard,
            AdditionalSpells = [Spells.LightshaperWardBuff],
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.RemoveMagic,
            AdditionalSpells = [Spells.RemoveMagicDispel],
            Category = SpellCategory.Utility,
            Gcd = StandardGcd,
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
