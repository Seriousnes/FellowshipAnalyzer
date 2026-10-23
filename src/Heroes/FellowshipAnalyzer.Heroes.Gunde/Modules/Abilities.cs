using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public class Abilities : CoreAbilities
{
    private const int BleedingHeartsHeartSplitterCharges = 2;

    /// <summary>
    /// Whether the player has Bleeding Hearts equipped, which gives Heart Splitter a second charge. An unowned
    /// spellbook reads the base ability.
    /// </summary>
    private bool BleedingHearts => Owner?.SelectedCombatant.HasLegendary(Legendaries.BleedingHearts.Id) == true;

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.DoubleStrike,
            AdditionalSpells = [Spells.DoubleStrikeDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = BleedingHearts
                ? Spells.HeartSplitter with { Charges = BleedingHeartsHeartSplitterCharges }
                : Spells.HeartSplitter,
            AdditionalSpells = [Spells.HeartSplitterDamage, Spells.HeartSplitterDotBonusDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.OwedInBlood,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.BloodArc,
            AdditionalSpells = [Spells.BloodArcDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.BloodboundSpirit,
            AdditionalSpells = [Spells.BloodboundSpiritDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.GrimCarve,
            AdditionalSpells = [Spells.GrimCarveDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.ReaverEdge,
            AdditionalSpells = [Spells.ReaverEdgeDamage],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Slaughter,
            AdditionalSpells = [Spells.SlaughterDot],
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Rupture,
            AdditionalSpells = [Spells.RuptureDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.ReignInBlood,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RecklessAbandon,
            Category = SpellCategory.Defensive,
            Gcd = null,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.ButcherHook,
            AdditionalSpells = [Spells.ButcherHookDamage],
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Jawbreaker,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Warbound,
            AdditionalSpells = [Spells.WarboundDamage],
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
