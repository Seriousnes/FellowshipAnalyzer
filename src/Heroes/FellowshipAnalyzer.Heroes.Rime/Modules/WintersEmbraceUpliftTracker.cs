using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed partial class WintersEmbraceUpliftTracker : EventSubscriber
{
    public const double DamageIncrease = 0.20;

    private readonly Dictionary<int, (string Name, long Bonus)> _bonusBySpell = [];

    private bool _embraceActive;

    public long TotalBonusDamage { get; private set; }

    public int AmplifiedEventCount { get; private set; }

    public long TotalDamage { get; private set; }

    public double UpliftShare => TotalDamage > 0 ? (double)TotalBonusDamage / TotalDamage : 0;

    public IReadOnlyList<SpellUplift> BySpell =>
    [
        .. _bonusBySpell
            .Select(entry => new SpellUplift(entry.Key, entry.Value.Name, entry.Value.Bonus))
            .OrderByDescending(uplift => uplift.BonusDamage)
            .ThenBy(uplift => uplift.Name, StringComparer.Ordinal)
    ];

    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnEmbraceApplied(ApplyBuffEvent applyBuffEvent) => _embraceActive = true;

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.WintersEmbrace))]
    private void OnEmbraceRemoved(RemoveBuffEvent removeBuffEvent) => _embraceActive = false;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        TotalDamage += damageEvent.Amount + (damageEvent.Absorbed ?? 0);

        if (!_embraceActive)
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

    public sealed record SpellUplift(int AbilityId, string Name, long BonusDamage);
}
