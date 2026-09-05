using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.AmendFate,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.EchoesOfRuin,
            AdditionalSpells = [Spells.EchoesOfRuinDot],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.EntropyClaim,
            AdditionalSpells = [Spells.EntropyClaimDot],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FlashRevision,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.Oblivion,
            AdditionalSpells = [Spells.OblivionDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.TemporalBarrage,
            AdditionalSpells = [Spells.TemporalBarrageDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.TimeShard,
            AdditionalSpells = [Spells.TimeShardDamage],
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.RestoreContinuity,
            Category = SpellCategory.RotationalAoe,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.EpochBreak,
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.FleetingHour,
            AdditionalSpells = [Spells.FleetingHourSelfBuff],
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CooldownStartsWhenBuffEnds = Spells.FleetingHourSelfBuff,
        },
        new()
        {
            PrimarySpell = Spells.UnfoldingDoom,
            AdditionalSpells = [Spells.UnfoldingDoomDamage],
            Category = SpellCategory.Cooldowns,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = CoreItems.TwilightSkybolt,
            Category = SpellCategory.Cooldowns,
            Gcd = null,
            CooldownReducedByHaste = true,
        },
        new()
        {
            PrimarySpell = Spells.Intercession,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.RevertMagic,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
        new()
        {
            PrimarySpell = Spells.TimeSkip,
            Category = SpellCategory.Utility,
            Gcd = null,
        },
    ];
}
