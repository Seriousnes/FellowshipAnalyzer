using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public class Abilities : CoreAbilities
{
    /// <summary>
    /// Primary spell id of the spellbook entry for <paramref name="spellId"/>: <see cref="Spells.Backstab"/>
    /// for <see cref="Spells.BackstabStealth"/>. An id outside the spellbook is returned unchanged.
    /// </summary>
    public int PrimarySpellIdOf(int spellId) => GetAbility(spellId)?.PrimarySpell.Id ?? spellId;

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.Backstab,
            AdditionalSpells = [Spells.BackstabStealth, Spells.BackstabDamageStrong, Spells.BackstabDamageWeak],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.WidowBite,
            AdditionalSpells = [Spells.WidowBiteDamageFirst, Spells.WidowBiteDamageSecond],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.QueenFang,
            AdditionalSpells = [Spells.QueenFangDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HemorrhagingStrike,
            AdditionalSpells = [Spells.HemorrhagingStrikeDamage, Spells.HemorrhagingStrikeBleed],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.SkitteringBlades,
            AdditionalSpells = [Spells.SkitteringBladesStealth, Spells.SkitteringBladesDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.ArachnidAssault,
            AdditionalSpells = [Spells.ArachnidAssaultDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.FinalStratagem,
            AdditionalSpells = [Spells.MacabreStratagem],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.MaidenOfDeath,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.MatriarchMacabre,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.ShadowWard,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.WebCrack,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Kick,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.StalkerStep,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.BroodingShadows,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            AdditionalSpells = [Spells.AttackDamage],
            Category = SpellCategory.Hidden,
        },
    ];
}
