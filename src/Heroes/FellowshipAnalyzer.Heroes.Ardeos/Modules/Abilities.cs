using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FireBall,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            CooldownReducedByHaste = true,
            AdditionalSpells = [Spells.FireBallDot],
        },
        new()
        {
            PrimarySpell = Spells.FireFrogs,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            AdditionalSpells = [Spells.FireFrogsDot],
        },
        new()
        {
            PrimarySpell = Spells.EngulfingFlames,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            AdditionalSpells = [Spells.EngulfingFlamesDot],
        },
        new()
        {
            PrimarySpell = Spells.SearingBlaze,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            AdditionalSpells = [Spells.SearingBlazeDot],
        },
        new()
        {
            PrimarySpell = Spells.Incinerate,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
            AdditionalSpells = [Spells.IncinerateDot],
        },
        new()
        {
            PrimarySpell = Spells.InfernalWave,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Wildfire,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Apocalypse,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
            AdditionalSpells = [Spells.ApocalypseDot],
        },
        new()
        {
            PrimarySpell = Spells.Pyromania,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Detonate,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FlameWard,
            Category = SpellCategory.Defensive,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Scorch,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Fireflash,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.Flickerstep,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
    ];
}
