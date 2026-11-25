using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FluttercallHeal,
            AdditionalSpells = [Spells.FluttercallHealHot],
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.FluttercallRestoreLife,
            AdditionalSpells = [Spells.FluttercallRestoreLifeHot, Spells.FluttercallRestoreLifeFlashHeal],
            Category = SpellCategory.Healing,
        },
        new()
        {
            PrimarySpell = Spells.Nettlebolt,
            AdditionalSpells = [Spells.NettleboltDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.PricklyVine,
            AdditionalSpells = [Spells.PricklyVineDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.Shroomsplosion,
            AdditionalSpells = [Spells.ShroomsplosionDamage],
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.EnfeeblingRootsap,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.HeartBloom,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.LifePetal,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.SafeHaven,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.RepositoryOfFrozenLight,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(CoreItems.IceshapersBrand.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.IronleafWard,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.FluttercallProtect,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.FluttercallEmbrace,
            Category = SpellCategory.Defensive,
            IsDefensive = true,
        },
        new()
        {
            PrimarySpell = Spells.CureAilment,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.FluttercallJubilee,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.HiddenTrail,
            Category = SpellCategory.Utility,
        },
    ];
}
