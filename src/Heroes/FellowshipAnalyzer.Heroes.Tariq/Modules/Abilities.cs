using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.ChainLightning,
            AdditionalSpells = [Spells.ChainLightningDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.CullingStrike,
            AdditionalSpells = [Spells.CullingStrikeDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.FaceBreaker,
            AdditionalSpells = [Spells.FaceBreakerAlt, Spells.FaceBreakerDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HeavyStrike,
            AdditionalSpells = [Spells.HeavyStrikeDamage, Spells.HeavyStrikeAoeDamage, Spells.HeavyStrikeCleaveDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.SkullCrusher,
            AdditionalSpells = [Spells.SkullCrusherDamage, Spells.SkullCrusherLightningDamage, Spells.Sledgehammer],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HammerStorm,
            AdditionalSpells = [Spells.HammerStormDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.WildSwing,
            AdditionalSpells = [Spells.WildSwingDamage],
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.FocusedWrath,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.RagingTempest,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.ThunderCall,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.UnbreakableWill,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.Intimidate,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.LeapSmash,
            AdditionalSpells = [Spells.LeapSmashDamage],
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Pummel,
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
