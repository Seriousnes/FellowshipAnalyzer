using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Emerald gem contributed. Sentinel's Bastion is the trait the log accounts for:
/// a shield reapplied on a timer, measured from the hits its absorb consumed. Emerald's other ranks are
/// expertise rating and percentage plus Blessing of the Commander's ability cooldown reduction, which
/// <see cref="StatTracker"/> folds into the cooldown pools rather than reporting as output.
/// </summary>
public sealed partial class EmeraldGemAnalyzer : Analyzer, IGemAnalyzer
{
    /// <summary>Reapplies a shield worth a share of the player's maximum health on a timer.</summary>
    public static readonly GemTrait SentinelsBastion = new(
        Items.SentinelsBastion, GemRankPower.Rank3,
        Items.SentinelsBastionII, GemRankPower.Rank8);

    /// <inheritdoc/>
    public GemType Gem => GemType.Emerald;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Emerald;

    /// <summary>Damage the Sentinel's Bastion shield absorbed over the fight.</summary>
    public long SentinelsBastionAbsorbed { get; private set; }

    /// <remarks>
    /// An absorb event names the absorbing ability, and Sentinel's Bastion exists as both an applying
    /// ability and an absorb effect under the same display name, so all four ids are matched.
    /// </remarks>
    [On<AbsorbedEvent>(To = Actor.Player)]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent)
    {
        var fslid = absorbedEvent.Ability.FSLID;
        if (fslid != Items.SentinelsBastion.FSLID &&
            fslid != Items.SentinelsBastionII.FSLID &&
            fslid != Items.SentinelsBastionShield.FSLID &&
            fslid != Items.SentinelsBastionShieldII.FSLID) return;

        SentinelsBastionAbsorbed += absorbedEvent.Amount;
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
