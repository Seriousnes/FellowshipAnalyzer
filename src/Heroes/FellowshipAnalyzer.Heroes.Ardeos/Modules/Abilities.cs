using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

public class Abilities : CoreAbilities
{
    /// <summary>Apocalypse charges with the Explosivo legendary (power 615) equipped.</summary>
    public static readonly int ExplosivoApocalypseCharges = (int)SpellTuning.Apocalypse.AddedCharges_MaxCharges;

    /// <summary>
    /// True when the Explosivo legendary is equipped. Apocalypse then has <see cref="ExplosivoApocalypseCharges"/>
    /// charges. An unowned spellbook reads the base ability.
    /// </summary>
    private bool Explosivo => Owner?.SelectedCombatant.HasLegendary(Legendaries.Explosivo.Id) == true;

    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.FireBall,
            Category = SpellCategory.Rotational,
            AdditionalSpells = [Spells.FireBallDot],
        },
        new()
        {
            PrimarySpell = Spells.FireFrogs,
            Category = SpellCategory.Rotational,
            AdditionalSpells = [Spells.FireFrogsDot],
        },
        new()
        {
            PrimarySpell = Spells.EngulfingFlames,
            Category = SpellCategory.Rotational,
            AdditionalSpells = [Spells.EngulfingFlamesDot],
        },
        new()
        {
            PrimarySpell = Spells.SearingBlaze,
            Category = SpellCategory.Rotational,
            AdditionalSpells = [Spells.SearingBlazeDot],
        },
        new()
        {
            PrimarySpell = Spells.Incinerate,
            Category = SpellCategory.Rotational,
            AdditionalSpells = [Spells.IncinerateDot],
        },
        new()
        {
            PrimarySpell = Spells.InfernalWave,
            Category = SpellCategory.Rotational,
        },
        new()
        {
            PrimarySpell = Spells.Wildfire,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Explosivo ? Spells.Apocalypse with { Charges = ExplosivoApocalypseCharges } : Spells.Apocalypse,
            Category = SpellCategory.Cooldowns,
            AdditionalSpells = [Spells.ApocalypseDot],
        },
        new()
        {
            PrimarySpell = Spells.Pyromania,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = Spells.Detonate,
            Category = SpellCategory.Cooldowns,
        },
        new()
        {
            PrimarySpell = CoreItems.Chronoshift,
            Category = SpellCategory.Cooldowns,
            Enabled = Owner?.SelectedCombatant.HasItem(CoreItems.AshasChronoshiftSpire_Ardeos.Id) == true,
        },
        new()
        {
            PrimarySpell = Spells.FlameWard,
            Category = SpellCategory.Defensive,
        },
        new()
        {
            PrimarySpell = Spells.Scorch,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Fireflash,
            Category = SpellCategory.Utility,
        },
        new()
        {
            PrimarySpell = Spells.Flickerstep,
            Category = SpellCategory.Utility,
        },
    ];
}
