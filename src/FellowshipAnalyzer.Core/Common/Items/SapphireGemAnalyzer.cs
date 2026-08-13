using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Sapphire gem contributed. Two of its five traits leave a trace in the log:
/// Ancestral Surge, a primary attribute buff active during Heroism whose windows the log records, and
/// Resonating Soul, a passive damage reduction that scales with missing health and is estimated from the
/// player's health at each hit they took. The remaining traits raise spirit and extend Heroism.
/// </summary>
public sealed partial class SapphireGemAnalyzer : Analyzer, IGemAnalyzer
{
    /// <summary>Raises the player's primary attribute while Heroism is active.</summary>
    public static readonly GemTrait AncestralSurge = new(
        Items.AncestralSurge, GemRankPower.Rank1,
        Items.AncestralSurgeII, GemRankPower.Rank6);

    /// <summary>
    /// Reduces all damage the player takes in proportion to the health they are missing: 1% for every 10%
    /// missing at rank 3, and 3% for every 10% missing at rank 8.
    /// </summary>
    public static readonly GemTrait ResonatingSoul = new(
        Items.ResonatingSoul, GemRankPower.Rank3,
        Items.ResonatingSoulII, GemRankPower.Rank8);

    /// <inheritdoc/>
    public GemType Gem => GemType.Sapphire;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Sapphire;

    /// <summary>Total time Ancestral Surge was active over the dungeon, in milliseconds.</summary>
    public int AncestralSurgeUptimeMs =>
        AncestralSurge.UptimeOn(Owner.SelectedCombatant, GemPower, Owner.DungeonStartTime, Owner.DungeonEndTime);

    /// <summary>Ancestral Surge's share of the dungeon, as a fraction.</summary>
    public double AncestralSurgeUptime =>
        Owner.DungeonDurationMs > 0 ? (double)AncestralSurgeUptimeMs / Owner.DungeonDurationMs : 0;

    /// <summary>Damage reduction the unlocked rank of Resonating Soul grants per 1% of health missing, as a fraction.</summary>
    public double ResonatingSoulReductionPerPercent =>
        ResonatingSoul.ByRank(GemPower, based: 0.001, upgraded: 0.003, locked: 0);

    /// <summary>Estimated damage Resonating Soul prevented over the dungeon.</summary>
    public long ResonatingSoulDamageReduced { get; private set; }

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent)
    {
        if (ResonatingSoulReductionPerPercent <= 0) return;
        if (damageEvent.TargetResources is not { MaxHitPoints: > 0 } resources) return;
        if (resources.HitPoints > resources.MaxHitPoints) return;

        var landed = damageEvent.Amount + (damageEvent.Absorbed ?? 0);
        if (landed <= 0) return;

        var healthBefore = Math.Min(resources.HitPoints + damageEvent.Amount, resources.MaxHitPoints);
        var missing = 1 - (double)healthBefore / resources.MaxHitPoints;
        var reduction = missing * 100 * ResonatingSoulReductionPerPercent;

        ResonatingSoulDamageReduced += (long)(landed * (reduction / (1 - reduction)));
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
