using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Topaz gem contributed. Rogue's Resurgence is the trait the log accounts for,
/// an emergency heal that fires when the player drops below half health. Topaz's other ranks raise haste
/// rating and percentage, which the log records only as already applied.
/// </summary>
public sealed partial class TopazGemAnalyzer : Analyzer, IGemAnalyzer
{
    /// <summary>Heals the player when they drop below half health, on its own cooldown.</summary>
    public static readonly GemTrait RoguesResurgence = new(
        Items.RoguesResurgence, GemRankPower.Rank3,
        Items.RoguesResurgenceII, GemRankPower.Rank8);

    /// <inheritdoc/>
    public GemType Gem => GemType.Topaz;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Topaz;

    /// <summary>Effective healing Rogue's Resurgence did over the dungeon.</summary>
    public long RoguesResurgenceHealing { get; private set; }

    /// <summary>Healing Rogue's Resurgence lost to overheal.</summary>
    public long RoguesResurgenceOverheal { get; private set; }

    [On<HealEvent>(To = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        if (healEvent.Ability.FSLID != Items.RoguesResurgence.FSLID &&
            healEvent.Ability.FSLID != Items.RoguesResurgenceII.FSLID) return;

        RoguesResurgenceHealing += healEvent.Amount;
        RoguesResurgenceOverheal += healEvent.Overheal ?? 0;
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
