using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.DoubleStrike,
            AdditionalSpells = [Spells.DoubleStrikeDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HeartSplitter,
            AdditionalSpells = [Spells.HeartSplitterDamage, Spells.HeartSplitterDotBonusDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.OwedInBlood,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.BloodArc,
            AdditionalSpells = [Spells.BloodArcDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.BloodboundSpirit,
            AdditionalSpells = [Spells.BloodboundSpiritDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.GrimCarve,
            AdditionalSpells = [Spells.GrimCarveDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.ReaverEdge,
            AdditionalSpells = [Spells.ReaverEdgeDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.Slaughter,
            AdditionalSpells = [Spells.SlaughterDot],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.Rupture,
            AdditionalSpells = [Spells.RuptureDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.ReignInBlood,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.RecklessAbandon,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.ButcherHook,
            AdditionalSpells = [Spells.ButcherHookDamage],
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Jawbreaker,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Warbound,
            AdditionalSpells = [Spells.WarboundDamage],
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Attack,
            Category = SpellCategory.Hidden,
        },
    ];
}
