using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Amethyst gem contributed. Two of its five traits leave a trace in the log:
/// Blessing of the Deathdealer, a passive critical power multiplier measured over the player's own crits,
/// and Reaper's Reprieve, a heal-over-time with its own heal events. The remaining traits are flat critical
/// strike rating and chance, which the log records only as already applied.
/// </summary>
public sealed partial class AmethystGemAnalyzer(Lazy<ThroughputTracker> throughputTracker)
    : Analyzer, IGemAnalyzer
{
    /// <summary>Increases the damage and healing of critical strikes, by 2% at rank 5 and 6% at rank 10.</summary>
    public static readonly GemTrait BlessingOfTheDeathdealer = new(
        Items.BlessingOfTheDeathdealer, GemRankPower.Rank5,
        Items.BlessingOfTheDeathdealerII, GemRankPower.Rank10);

    /// <summary>Leaves a heal-over-time on the player whenever an enemy they damaged dies.</summary>
    public static readonly GemTrait ReapersReprieve = new(
        Items.ReapersReprieve, GemRankPower.Rank3,
        Items.ReapersReprieveII, GemRankPower.Rank8);

    /// <inheritdoc/>
    public GemType Gem => GemType.Amethyst;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Amethyst;

    /// <inheritdoc/>
    public ThroughputTracker Throughput => throughputTracker.Value;

    /// <summary>The critical power bonus the unlocked rank of Blessing of the Deathdealer grants, as a fraction.</summary>
    public double CriticalPowerBonus =>
        BlessingOfTheDeathdealer.ByRank(GemPower, based: 0.02, upgraded: 0.06, locked: 0);

    /// <summary>Total critical strike damage the player dealt, with the critical power bonus already included.</summary>
    public long CriticalDamage { get; private set; }

    /// <summary>Total effective critical healing the player did, with the critical power bonus already included.</summary>
    public long CriticalHealing { get; private set; }

    /// <summary>
    /// The share of <see cref="CriticalDamage"/> Blessing of the Deathdealer was responsible for. The logged
    /// amount already includes the bonus, so the share it added is <c>amount × bonus ÷ (1 + bonus)</c>.
    /// </summary>
    public long CriticalPowerDamage => ContributedShare(CriticalDamage);

    /// <summary>The share of <see cref="CriticalHealing"/> Blessing of the Deathdealer was responsible for.</summary>
    public long CriticalPowerHealing => ContributedShare(CriticalHealing);

    /// <summary>Effective healing Reaper's Reprieve did over the fight.</summary>
    public long ReapersReprieveHealing { get; private set; }

    /// <summary>Healing Reaper's Reprieve lost to overheal.</summary>
    public long ReapersReprieveOverheal { get; private set; }

    private long ContributedShare(long amount) =>
        CriticalPowerBonus <= 0 ? 0 : (long)(amount * (CriticalPowerBonus / (1 + CriticalPowerBonus)));

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        if (damageEvent.IsCritical)
            CriticalDamage += damageEvent.Amount;
    }

    [On<HealEvent>(By = Actor.Player)]
    private void OnCriticalHeal(HealEvent healEvent)
    {
        if (healEvent.HitType is HitType.Crit or HitType.GreviousCrit)
            CriticalHealing += healEvent.Amount;
    }

    [On<HealEvent>(To = Actor.Player)]
    private void OnReapersReprieveHeal(HealEvent healEvent)
    {
        if (healEvent.Ability.FSLID != Items.ReapersReprieve.FSLID &&
            healEvent.Ability.FSLID != Items.ReapersReprieveII.FSLID) return;

        ReapersReprieveHealing += healEvent.Amount;
        ReapersReprieveOverheal += healEvent.Overheal ?? 0;
    }

    /// <inheritdoc/>
    public override Type? StatisticsComponentType =>
        BlessingOfTheDeathdealer.IsUnlocked(GemPower) || ReapersReprieve.IsUnlocked(GemPower)
            ? typeof(AmethystGemStatistics)
            : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
