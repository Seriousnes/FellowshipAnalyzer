using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.Utility;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Accounts what the Winter's Embrace amplifier was worth over the whole fight. Winter's Embrace is
/// baseline: while it stands the player deals 20% more damage with everything except Bursting Ice
/// itself, so every other hit inside a window carries a share attributable to the buff. Window
/// execution is the guide's subject; this module answers only how much damage the amplifier added
/// and which spells collected it.
/// </summary>
/// <remarks>
/// Attribution is marginal, as <see cref="CombatMath.CalculateEffectiveDamage"/> defines it: a hit of
/// 1200 inside a window reads as 1000 base plus 200 from the amplifier. Absorbed damage counts
/// towards the raw hit, so a fully absorbed hit still contributes its share. Bursting Ice and its
/// damage effect are excluded because the buff does not amplify them.
/// <para>
/// The buff is tracked from the player's own apply/remove pair. A re-apply while a window already
/// stands leaves accrual running, and a removal with no window standing is ignored, so a log missing
/// either half of a pair cannot double-count or strand the accumulator.
/// </para>
/// </remarks>
public sealed partial class WintersEmbraceUpliftTracker : EventSubscriber
{
    /// <summary>The added damage multiplier Winter's Embrace grants.</summary>
    public const double DamageIncrease = 0.20;

    private readonly Dictionary<int, (string Name, long Bonus)> _bonusBySpell = [];

    private bool _embraceStanding;

    /// <summary>Damage attributable to the Winter's Embrace amplifier across the fight.</summary>
    public long TotalBonusDamage { get; private set; }

    /// <summary>Player damage events the amplifier applied to.</summary>
    public int AmplifiedEventCount { get; private set; }

    /// <summary>All player damage this fight, raw of absorbs, amplified or not.</summary>
    public long TotalDamage { get; private set; }

    /// <summary>The amplifier's share of <see cref="TotalDamage"/>.</summary>
    public double UpliftShare => TotalDamage > 0 ? (double)TotalBonusDamage / TotalDamage : 0;

    /// <summary>Per-spell attribution, heaviest contributor first.</summary>
    public IReadOnlyList<SpellUplift> BySpell =>
    [
        .. _bonusBySpell
            .Select(entry => new SpellUplift(entry.Key, entry.Value.Name, entry.Value.Bonus))
            .OrderByDescending(uplift => uplift.BonusDamage)
            .ThenBy(uplift => uplift.Name, StringComparer.Ordinal)
    ];

    public override Type? StatisticsComponentType =>
        AmplifiedEventCount > 0 ? typeof(WintersEmbraceStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnEmbraceApplied(ApplyBuffEvent applyBuffEvent) => _embraceStanding = true;

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnEmbraceRemoved(RemoveBuffEvent removeBuffEvent) => _embraceStanding = false;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        TotalDamage += damageEvent.Amount + (damageEvent.Absorbed ?? 0);

        if (!_embraceStanding)
            return;

        if (damageEvent.Ability.Id == Spells.BurstingIce.FSLID ||
            damageEvent.Ability.Id == Spells.BurstingIceDamage.FSLID)
            return;

        var bonus = CombatMath.CalculateEffectiveDamage(damageEvent, DamageIncrease);
        TotalBonusDamage += bonus;
        AmplifiedEventCount++;

        var id = damageEvent.Ability.Id;
        _bonusBySpell[id] = _bonusBySpell.TryGetValue(id, out var existing)
            ? (existing.Name, existing.Bonus + bonus)
            : (damageEvent.Ability.Name, bonus);
    }

    /// <summary>One spell's share of the amplifier's damage.</summary>
    /// <param name="AbilityId">The FSL id of the amplified ability or effect.</param>
    /// <param name="Name">The ability name as the log carried it.</param>
    /// <param name="BonusDamage">Damage on that ability attributable to the amplifier.</param>
    public sealed record SpellUplift(int AbilityId, string Name, long BonusDamage);
}
