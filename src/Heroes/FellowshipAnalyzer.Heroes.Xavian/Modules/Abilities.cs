using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.BrilliantFlash,
            AdditionalSpells = [Spells.BrilliantFlashDamage, Spells.BrilliantFlashHeal],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.SunStrike,
            AdditionalSpells = [Spells.SunStrikeDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.BrilliantFlare,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.BlindingSlash,
            AdditionalSpells = [Spells.BlindingSlashDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.Omnistrike,
            AdditionalSpells = [Spells.OmnistrikeDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.SolarBlades,
            AdditionalSpells = [Spells.SolarBladesDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.ShiningHalo,
            AdditionalSpells = [Spells.ShiningHaloDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.SkyCrash,
            AdditionalSpells = [Spells.SkyCrashDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.OmegaReprieval,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.DecreeOfTheSun,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.SolarShield,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.AuraOfSolace,
            Category = SpellCategory.Defensive,
        },
        new()
        {
            PrimarySpell = Spells.Interrupt,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.RupturedDawn,
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
            AdditionalSpells = [Spells.AttackDamage],
            Category = SpellCategory.Hidden,
        },
    ];
}
