using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.Dawnflare,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.Soulbrand,
            AdditionalSpells = [Spells.SoulbrandDot, Spells.SoulbrandBuffActiveAdditionalDirectDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.RuneOfRenewal,
            AdditionalSpells = [Spells.RuneOfRenewalBuff, Spells.RuneOfRenewalEcho],
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.LuminousBarrier,
            AdditionalSpells = [Spells.LuminousBarrierAbsorb],
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.RadiantBlast,
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.DawnbreakerOrb,
            AdditionalSpells = [Spells.MeticulousRunesmithAbsorb],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.CircleOfLight,
            AdditionalSpells = [Spells.CeremonyOfLightBuff],
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.GreaterHeal,
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.AvatarOfLight,
            AdditionalSpells = [Spells.AvatarOfLightBuff],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.RunicProliferation,
            AdditionalSpells = [Spells.RunicProliferationBuff],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.RepositoryOfFrozenLight,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(CoreItems.AncientIceshapersTome.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.LightshaperWard,
            AdditionalSpells = [Spells.LightshaperWardBuff],
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.RemoveMagic,
            AdditionalSpells = [Spells.RemoveMagicDispel],
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.ThrowBook,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Levitate,
            Category = SpellCategory.Utility,
        },
    ];
}
