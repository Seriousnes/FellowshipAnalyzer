using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FocusedShot,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.CelestialShot,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.Multishot,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HighwindArrow,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.HeartseekerBarrage,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.LunarlightMark,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.StarfallVolley,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersSupremacy,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.SkystridersGrace,
            Category = SpellCategory.Cooldowns,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.EventHorizon,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.Chronoshift,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(CoreItems.AshasChronoshiftBow.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.CelestialVeil,
            Category = SpellCategory.Defensive,
        },
        new()
        {
            PrimarySpell = Spells.Roll,
            Category = SpellCategory.Utility,
            CastableWhileCasting = true,
        },
        new()
        {
            PrimarySpell = Spells.Disrupt,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.GrapplingArrow,
            Category = SpellCategory.Utility,
        },
    ];
}
