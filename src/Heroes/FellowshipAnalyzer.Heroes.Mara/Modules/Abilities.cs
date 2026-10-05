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
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.WidowBite,
            AdditionalSpells = [Spells.WidowBiteDamageFirst, Spells.WidowBiteDamageSecond],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.QueenFang,
            AdditionalSpells = [Spells.QueenFangDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.HemorrhagingStrike,
            AdditionalSpells = [Spells.HemorrhagingStrikeDamage, Spells.HemorrhagingStrikeBleed],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.SkitteringBlades,
            AdditionalSpells = [Spells.SkitteringBladesStealth, Spells.SkitteringBladesDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.ArachnidAssault,
            AdditionalSpells = [Spells.ArachnidAssaultDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FinalStratagem,
            AdditionalSpells = [Spells.MacabreStratagem],
            Category = SpellCategory.Cooldowns,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.MaidenOfDeath,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.MatriarchMacabre,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.ShadowWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.WebCrack,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Kick,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.StalkerStep,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.BroodingShadows,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            AdditionalSpells = [Spells.AttackDamage],
            Category = SpellCategory.Hidden,
        },
    ];
}
