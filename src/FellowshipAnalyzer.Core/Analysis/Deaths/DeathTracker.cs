using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Deaths;

/// <summary>
/// Dungeon-lifetime capture of every death of the analyzed player. Incoming hits and healing accumulate
/// into a rolling buffer trimmed to <see cref="RecapWindowMs"/>, so a long dungeon keeps only the lead-up
/// it might need; a death snapshots that buffer plus everything only knowable during dispatch, and
/// <see cref="PlayerDeath"/> derives the rest from the retained state.
/// <para>
/// Cooldown availability has to be captured here rather than derived afterwards: <see cref="SpellUsable"/>
/// answers only for the current dispatch position, and by the end of the parse that position is the end of
/// the dungeon. Aura state, by contrast, is history the parse leaves behind, so the same snapshot could read
/// it later; it is taken here to keep <see cref="PlayerDeath"/> free of live module references.
/// </para>
/// <para>
/// Casts are counted here rather than read off <see cref="SpellUsable.Casts"/>, which holds one entry
/// per <see cref="CastEvent"/>. A cast-time ability logs two: the activation, carrying
/// <see cref="CastEvent.Activation"/>, and the completion roughly a cast time later without it. An
/// instant ability logs only the activation. Counting every entry therefore counts a cast-time activation
/// twice, so a completion arriving within <see cref="CastCompletionPairingMs"/> of an unconsumed
/// activation of the same ability is folded into it. A completion with no activation to fold into still
/// counts, so a log that never sets the flag loses nothing.
/// </para>
/// </summary>
[Dependency<SpellUsable>]
[Dependency<Abilities>]
public sealed partial class DeathTracker : Analyzer
{
    /// <summary>How far back a death's captured window reaches, in milliseconds, before the pull-start clamp.</summary>
    public const int RecapWindowMs = 15_000;

    /// <summary>
    /// How long after an activation a completion may arrive and still be folded into it. Comfortably above
    /// any cast time in the game, so a genuinely separate activation of the same ability is never swallowed.
    /// </summary>
    public const int CastCompletionPairingMs = 6_000;

    private readonly List<IncomingHit> _hits = [];
    private readonly List<HealTick> _heals = [];
    private readonly List<PlayerCast> _casts = [];
    private readonly List<PlayerDeath> _deaths = [];
    private readonly Dictionary<int, int> _activationAwaitingCompletion = [];

    /// <summary>Every death of the analyzed player, in the order they happened.</summary>
    public List<PlayerDeath> Deaths => _deaths;

    /// <summary>The deaths that fall inside <paramref name="pull"/>, or every death when it is <c>null</c>.</summary>
    public List<PlayerDeath> For(PullStartEvent? pull) =>
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

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        Trim(castEvent.Timestamp);

        var abilityId = castEvent.Ability.Id;

        if (castEvent.Activation)
        {
            _activationAwaitingCompletion[abilityId] = castEvent.Timestamp;
        }
        else if (_activationAwaitingCompletion.TryGetValue(abilityId, out var activatedAt)
                 && castEvent.Timestamp - activatedAt <= CastCompletionPairingMs)
        {
            _activationAwaitingCompletion.Remove(abilityId);
            return;
        }

        _casts.Add(new PlayerCast(castEvent.Timestamp, abilityId));
    }

    [On<DeathEvent>(To = Actor.Player)]
    private void OnDeath(DeathEvent deathEvent)
    {
        var pull = Owner.CurrentPull;
        var floor = pull?.StartTime ?? Owner.DungeonStartTime;
        var windowStart = Math.Max(deathEvent.Timestamp - RecapWindowMs, floor);

        List<IncomingHit> hits = [.. _hits.Where(hit => hit.Timestamp >= windowStart && hit.Timestamp <= deathEvent.Timestamp)];

        long healing = 0;
        foreach (var heal in _heals)
        {
            if (heal.Timestamp >= windowStart && heal.Timestamp <= deathEvent.Timestamp)
                healing += heal.Amount;
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
            Defensives = CaptureDefensives(windowStart, deathEvent.Timestamp),
        });
    }

    private List<DefensiveReadiness> CaptureDefensives(int windowStart, int deathTimestamp)
    {
        var castsInWindow = new Dictionary<int, (int Count, int Last)>();
        foreach (var cast in _casts)
        {
            if (cast.Timestamp < windowStart || cast.Timestamp > deathTimestamp) continue;
            if (Abilities.GetAbility(cast.AbilityId) is not { } ability) continue;

            var key = ability.PrimarySpell.FSLID.Value;
            var (Count, Last) = castsInWindow.GetValueOrDefault(key);
            castsInWindow[key] = (Count + 1, cast.Timestamp);
        }

        List<DefensiveReadiness> defensives = [];
        foreach (var ability in Abilities.GetAbilities())
        {
            if (!IsMitigationOrHealing(ability) || ability.GetCooldown() <= 0) continue;

            var primaryId = ability.PrimarySpell.FSLID.Value;
            var (Count, Last) = castsInWindow.GetValueOrDefault(primaryId);

            defensives.Add(new DefensiveReadiness(
                ability,
                SpellUsable.ChargesAvailable(primaryId),
                Count,
                Count > 0 ? Last : null,
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
        if (Owner.SelectedCombatant.HasBuff(ability.PrimarySpell, timestamp)) return true;

        if (ability.AdditionalSpells is not { } extras) return false;

        foreach (var extra in extras)
        {
            if (Owner.SelectedCombatant.HasBuff(extra, timestamp)) return true;
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

        var expiredCasts = 0;
        while (expiredCasts < _casts.Count && _casts[expiredCasts].Timestamp < cutoff) expiredCasts++;
        if (expiredCasts > 0) _casts.RemoveRange(0, expiredCasts);
    }

    private readonly record struct HealTick(int Timestamp, long Amount);

    private readonly record struct PlayerCast(int Timestamp, int AbilityId);
}
