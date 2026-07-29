using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Deaths;

/// <summary>
/// Fight-lifetime capture of every death of the analyzed player. Incoming hits and healing accumulate
/// into a rolling buffer trimmed to <see cref="RecapWindowMs"/>, so a long fight keeps only the lead-up
/// it might need; a death snapshots that buffer plus everything only knowable during dispatch, and
/// <see cref="PlayerDeath"/> derives the rest from the retained state.
/// <para>
/// Cooldown availability has to be captured here rather than derived afterwards: <see cref="SpellUsable"/>
/// answers only for the current dispatch position, and by the end of the parse that position is the end of
/// the fight. Aura state, by contrast, is history the parse leaves behind, so the same snapshot could read
/// it later; it is taken here to keep <see cref="PlayerDeath"/> free of live module references.
/// </para>
/// </summary>
[Dependency<SpellUsable>]
[Dependency<Abilities>]
public sealed partial class DeathTracker : EventSubscriber
{
    /// <summary>How far back a death's captured window reaches, in milliseconds, before the pull-start clamp.</summary>
    public const int RecapWindowMs = 15_000;

    private readonly List<IncomingHit> _hits = [];
    private readonly List<HealTick> _heals = [];
    private readonly List<PlayerDeath> _deaths = [];

    /// <summary>Every death of the analyzed player, in the order they happened.</summary>
    public IReadOnlyList<PlayerDeath> Deaths => _deaths;

    /// <summary>The deaths that fall inside <paramref name="pull"/>, or every death when it is <c>null</c>.</summary>
    public IReadOnlyList<PlayerDeath> For(Pull? pull) =>
        pull is null ? _deaths : [.. _deaths.Where(death => ReferenceEquals(death.Pull, pull))];

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent)
    {
        Trim(damageEvent.Timestamp);

        _hits.Add(new IncomingHit(
            damageEvent.Timestamp,
            damageEvent.Ability,
            damageEvent.SourceId,
            damageEvent.Amount,
            damageEvent.UnmitigatedAmount,
            damageEvent.Mitigated,
            damageEvent.Absorbed ?? 0,
            damageEvent.Blocked,
            damageEvent.HitType,
            damageEvent.TargetResources?.HitPoints,
            damageEvent.TargetResources?.MaxHitPoints,
            damageEvent.TargetResources?.Absorb));
    }

    [On<HealEvent>(To = Actor.Player)]
    private void OnHealed(HealEvent healEvent)
    {
        Trim(healEvent.Timestamp);
        _heals.Add(new HealTick(healEvent.Timestamp, healEvent.Amount));
    }

    [On<DeathEvent>(To = Actor.Player)]
    private void OnDeath(DeathEvent deathEvent)
    {
        var pull = Owner.CurrentPull;
        var floor = pull?.StartTime ?? Owner.FightStartTime;
        var windowStart = Math.Max(deathEvent.Timestamp - RecapWindowMs, floor);

        List<IncomingHit> hits = [.. _hits.Where(hit => hit.Timestamp >= windowStart && hit.Timestamp <= deathEvent.Timestamp)];

        long healing = 0;
        foreach (var heal in _heals)
        {
            if (heal.Timestamp >= windowStart && heal.Timestamp <= deathEvent.Timestamp)
                healing += heal.Amount;
        }

        long? maxHitPoints = null;
        for (var i = hits.Count - 1; i >= 0; i--)
        {
            if (hits[i].MaxHitPoints is not { } max) continue;
            maxHitPoints = max;
            break;
        }

        _deaths.Add(new PlayerDeath
        {
            Timestamp = deathEvent.Timestamp,
            Pull = pull,
            KillingBlow = deathEvent.Ability,
            KillerId = deathEvent.SourceId,
            WindowStart = windowStart,
            Hits = hits,
            HealingReceived = healing,
            MaxHitPoints = maxHitPoints,
            Defensives = CaptureDefensives(windowStart, deathEvent.Timestamp),
        });
    }

    private List<DefensiveReadiness> CaptureDefensives(int windowStart, int deathTimestamp)
    {
        var castsInWindow = new Dictionary<int, (int Count, int Last)>();
        foreach (var cast in SpellUsable.Casts)
        {
            if (cast.Timestamp < windowStart || cast.Timestamp > deathTimestamp) continue;
            if (Abilities.GetAbility(cast.Id) is not { } ability) continue;

            var key = ability.PrimarySpell.FSLID.Value;
            var entry = castsInWindow.GetValueOrDefault(key);
            castsInWindow[key] = (entry.Count + 1, cast.Timestamp);
        }

        List<DefensiveReadiness> defensives = [];
        foreach (var ability in Abilities.GetAbilities())
        {
            if (!IsMitigationOrHealing(ability) || ability.GetCooldown() <= 0) continue;

            var primaryId = ability.PrimarySpell.FSLID.Value;
            var casts = castsInWindow.GetValueOrDefault(primaryId);

            defensives.Add(new DefensiveReadiness(
                ability,
                SpellUsable.ChargesAvailable(primaryId),
                casts.Count,
                casts.Count > 0 ? casts.Last : null,
                IsActiveOnPlayer(ability, deathTimestamp)));
        }

        defensives.Sort(static (left, right) => string.Compare(
            left.Ability.Name ?? left.Ability.PrimarySpell.Name,
            right.Ability.Name ?? right.Ability.PrimarySpell.Name,
            StringComparison.OrdinalIgnoreCase));

        return defensives;
    }

    private static bool IsMitigationOrHealing(SpellbookAbility ability) =>
        ability.IsDefensive || ability.Category is SpellCategory.Defensive or SpellCategory.Healing;

    private bool IsActiveOnPlayer(SpellbookAbility ability, int timestamp)
    {
        if (Owner.SelectedCombatant.HasBuff(ability.PrimarySpell.FSLID.Value, timestamp)) return true;

        if (ability.AdditionalSpells is not { } extras) return false;

        foreach (var extra in extras)
        {
            if (Owner.SelectedCombatant.HasBuff(extra.FSLID.Value, timestamp)) return true;
        }

        return false;
    }

    private void Trim(int timestamp)
    {
        var cutoff = timestamp - RecapWindowMs;

        var expiredHits = 0;
        while (expiredHits < _hits.Count && _hits[expiredHits].Timestamp < cutoff) expiredHits++;
        if (expiredHits > 0) _hits.RemoveRange(0, expiredHits);

        var expiredHeals = 0;
        while (expiredHeals < _heals.Count && _heals[expiredHeals].Timestamp < cutoff) expiredHeals++;
        if (expiredHeals > 0) _heals.RemoveRange(0, expiredHeals);
    }

    private readonly record struct HealTick(int Timestamp, long Amount);
}
