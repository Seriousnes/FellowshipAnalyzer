using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Ruby gem contributed. Unyielding Vitality is the trait the log accounts for,
/// a periodic in-combat health regeneration with its own heal events. Ruby's other ranks raise primary
/// attribute, stamina, and maximum health, which the log records only as already applied.
/// </summary>
public sealed partial class RubyGemAnalyzer(Lazy<ThroughputTracker> throughputTracker)
    : Analyzer, IGemAnalyzer
{
    /// <summary>Regenerates a share of the player's health on a timer while they are in combat.</summary>
    public static readonly GemTrait UnyieldingVitality = new(
        Items.UnyieldingVitality, GemRankPower.Rank3,
        Items.UnyieldingVitalityII, GemRankPower.Rank8);

    /// <inheritdoc/>
    public GemType Gem => GemType.Ruby;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Ruby;

    /// <inheritdoc/>
    public ThroughputTracker Throughput => throughputTracker.Value;

    /// <summary>Effective healing Unyielding Vitality did over the fight.</summary>
    public long UnyieldingVitalityHealing { get; private set; }

    /// <summary>Healing Unyielding Vitality lost to overheal.</summary>
    public long UnyieldingVitalityOverheal { get; private set; }

    [On<HealEvent>(To = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        if (healEvent.Ability.FSLID != Items.UnyieldingVitality.FSLID &&
            healEvent.Ability.FSLID != Items.UnyieldingVitalityII.FSLID) return;

        UnyieldingVitalityHealing += healEvent.Amount;
        UnyieldingVitalityOverheal += healEvent.Overheal ?? 0;
    }

    /// <inheritdoc/>
    public override Type? StatisticsComponentType =>
        UnyieldingVitality.IsUnlocked(GemPower) ? typeof(RubyGemStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
