using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using Items = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.BrainFreeze,
            Category = SpellCategory.Utility,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.BurstingIce,
            AdditionalSpells = [Spells.BurstingIceDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.ColdSnap,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.FlightOfTheNavir,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.FreezingTorrent,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.FrigidWinds,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.FrostBolt,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.FrostWard,
            Category = SpellCategory.Defensive,
        },
        new()
        {
            PrimarySpell = Spells.GlacialBlast,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.IceBlitz,
            Category = SpellCategory.Cooldowns,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.IceComet,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.IceDash,
            Category = SpellCategory.Defensive,
        },
        new()
        {
            PrimarySpell = Spells.WintersBlessing,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.WrathOfWinter,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Items.Chronoshift,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(Items.AshasChronoshiftSpire_Rime.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.FrostSwallows,
            AdditionalSpells = [Spells.FrostSwallowsDamage],
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Items.VoidbringerTouch,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Core.Common.Spells.Spells.Kindling,
            Category = SpellCategory.Hidden,
        },
        new()
        {
            PrimarySpell = Spells.RisingTalons,
            Category = SpellCategory.RotationalAoe,
        },
        new()
        {
            PrimarySpell = Spells.TalonStrike,
            Category = SpellCategory.Rotational,
        },
    ];
}
