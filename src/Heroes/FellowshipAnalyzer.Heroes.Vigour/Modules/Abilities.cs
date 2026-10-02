using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using VigourTalents = FellowshipAnalyzer.Core.Common.Spells.VigourTalents;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public class Abilities : CoreAbilities
{
    public const double RadiantBlastCooldown = 20 / 3d;
    public const double DawnbreakerOrbCooldown = 20 / 3d;
    public const double CircleOfLightCooldown = 10;
    public const double LuminousBarrierCooldown = 40 / 3d;
    public const double SacredBarrierCooldown = 20;

    private bool SacredBarrier => Owner?.SelectedCombatant.HasTalent(VigourTalents.SacredBarrier) == true;

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
            PrimarySpell = Spells.LuminousBarrier with
            {
                Cooldown = SacredBarrier ? SacredBarrierCooldown : LuminousBarrierCooldown,
                Charges = SacredBarrier ? 2 : 1,
            },
            AdditionalSpells = [Spells.LuminousBarrierAbsorb],
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.RadiantBlast with { Cooldown = RadiantBlastCooldown },
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.DawnbreakerOrb with { Cooldown = DawnbreakerOrbCooldown },
            AdditionalSpells = [Spells.MeticulousRunesmithAbsorb],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.CircleOfLight with { Cooldown = CircleOfLightCooldown },
            AdditionalSpells = [Spells.CeremonyOfLightBuff],
            Category = SpellCategory.Healing,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
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
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.LightshaperWard,
            AdditionalSpells = [Spells.LightshaperWardBuff],
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
            CooldownReducedByHaste = true,
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
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Levitate,
            Category = SpellCategory.Utility,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
    ];
}
