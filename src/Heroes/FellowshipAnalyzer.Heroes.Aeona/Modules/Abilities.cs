using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

public class Abilities : CoreAbilities
{
    /// <summary>
    /// Maximum charges of Entropy's Claim for this player. The cast normalizer builds a spellbook before any
    /// module has an owner, so an unowned spellbook reads the base charge.
    /// </summary>
    private int EntropyClaimCharges => Owner?.GetModule<AeonaBuild>()?.EntropyClaimCharges ?? 1;

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.AmendFate,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.EchoesOfRuin,
            AdditionalSpells = [Spells.EchoesOfRuinDot],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.EntropyClaim with { Charges = EntropyClaimCharges },
            IndependentCharges = true,
            AdditionalSpells = [Spells.EntropyClaimDot],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.FlashRevision,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.Oblivion,
            AdditionalSpells = [Spells.OblivionDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.TemporalBarrage,
            AdditionalSpells = [Spells.TemporalBarrageDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.TimeShard,
            AdditionalSpells = [Spells.TimeShardDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.RestoreContinuity,
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.EpochBreak,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.FleetingHour,
            AdditionalSpells = [Spells.FleetingHourSelfBuff],
            Category = SpellCategory.Cooldowns,
            CooldownStartsWhenBuffEnds = Spells.FleetingHourSelfBuff,
        },
        new()
        {
            PrimarySpell = Spells.UnfoldingDoom,
            AdditionalSpells = [Spells.UnfoldingDoomDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.TwilightSkybolt,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.RepositoryOfFrozenLight,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(CoreItems.FragmentsOfFrozenTime.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.Intercession,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.RevertMagic,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.TimeSkip,
            Category = SpellCategory.Utility,
        },
    ];
}
