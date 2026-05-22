using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks spell cooldown state during event dispatch, fabricating
/// <see cref="UpdateSpellUsableEvent"/> events when spells go on/off cooldown.
/// Also tracks all player casts (replacing the former TrackedStateModule).
/// </summary>
public sealed partial class SpellUsable(Lazy<Abilities> abilities, Lazy<DebugAnnotations> debugAnnotations, Lazy<Haste> haste) : Analyzer
{
    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];

    private double _globalRateMultiplier = 1.0;
    private readonly Dictionary<int, double> _spellRateMultipliers = [];

    private int _nextLeaseId = 1;
    private readonly Dictionary<int, HasteScaledLease> _hasteScaledLeases = [];

    public IReadOnlyList<TrackedAbilityCast> Casts => _casts;

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public IReadOnlyCollection<int> GetSpellsOnCooldown() => _cooldowns.Keys;

    /// <summary>
    /// Reduces the remaining cooldown of a spell by up to <paramref name="milliseconds"/>.
    /// For multi-charge spells, properly restores each charge in sequence as CDR overflows
    /// into the next, without requiring a workaround for skipping multiple charges at once.
    /// </summary>
    /// <returns>The actual amount of CDR applied in milliseconds.</returns>
    public int ReduceCooldown(int spellId, int milliseconds, int timestamp)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd) || milliseconds <= 0)
            return 0;

        var remaining = Math.Max(0, cd.ExpectedEnd - timestamp);

        if (milliseconds < remaining)
        {
            _cooldowns[spellId] = cd with { ExpectedEnd = cd.ExpectedEnd - milliseconds };
            return milliseconds;
        }

        EndCooldown(spellId, timestamp);
        return remaining + ReduceCooldown(spellId, milliseconds - remaining, timestamp);
    }

    public bool IsAvailable(int spellId) => !_cooldowns.TryGetValue(spellId, out var cd) || cd.ChargesAvailable > 0;

    public bool IsOnCooldown(int spellId) => _cooldowns.ContainsKey(spellId);

    public int ChargesAvailable(int spellId) =>
        _cooldowns.TryGetValue(spellId, out var cd)
            ? cd.ChargesAvailable
            : _abilities.GetMaxCharges(spellId);

    public int CooldownRemaining(int spellId, int? atTimestamp = null)
    {
        var ts = atTimestamp ?? this.Owner.CurrentTimestamp;
        return _cooldowns.TryGetValue(spellId, out var cd)
            ? Math.Max(0, cd.ExpectedEnd - ts)
            : 0;
    }

    public void BeginCooldown(int spellId, int timestamp)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var cdDuration = (int)(baseDurationMs / EffectiveRate(spellId));

            var maxCharges = _abilities.GetMaxCharges(spellId);
            cd = new CooldownInfo(
                OverallStart: timestamp,
                ChargeStart: timestamp,
                ExpectedEnd: timestamp + cdDuration,
                RechargeDuration: cdDuration,
                ChargesAvailable: maxCharges - 1,
                MaxCharges: maxCharges);
            _cooldowns[spellId] = cd;

            FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, timestamp, cd);
        }
        else if (cd.ChargesAvailable > 0)
        {
            cd = cd with { ChargesAvailable = cd.ChargesAvailable - 1 };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.UseCharge, spellId, timestamp, cd);
        }
        else
        {
            EndCooldown(spellId, timestamp);
            BeginCooldown(spellId, timestamp);
        }
    }

    public void EndCooldown(int spellId, int timestamp, bool restoreAllCharges = false)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return;

        cd = restoreAllCharges
            ? cd with { ChargesAvailable = cd.MaxCharges, ExpectedEnd = timestamp }
            : cd with { ChargesAvailable = cd.ChargesAvailable + 1 };

        if (cd.ChargesAvailable >= cd.MaxCharges)
        {
            cd = cd with { ExpectedEnd = timestamp };
            FabricateUpdate(UpdateSpellUsableType.EndCooldown, spellId, timestamp, cd);
            _cooldowns.Remove(spellId);
        }
        else
        {
            var nextEnd = timestamp + cd.RechargeDuration;
            cd = cd with { ChargeStart = timestamp, ExpectedEnd = nextEnd };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.RestoreCharge, spellId, timestamp, cd);
        }
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        _casts.Add(new TrackedAbilityCast(e.Timestamp, e.Ability.Id, e.TargetId));

        if (_abilities.GetAbility(e.Ability.Id) is null)
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: "#e67e22",
                Summary: $"Unconfigured spell: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: "This spell was cast by the player but is not in the hero's spellbook. " +
                         "Consider adding it to the Abilities module."));
        }

        if (!IsAvailable(e.Ability.Id))
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: "#e74c3c",
                Summary: $"Cast while on cooldown: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: $"{CooldownRemaining(e.Ability.Id, e.Timestamp)}ms remaining at time of cast.",
                Priority: 10));
        }

        BeginCooldown(e.Ability.Id, e.Timestamp);
    }

    [On<FilterCooldownInfoEvent>(By = Actor.Player)]
    private void OnFilterCooldown(FilterCooldownInfoEvent e) =>
        BeginCooldown(e.Ability.Id, e.Timestamp);

    [On<Event>]
    private void OnAnyEvent(Event e) => AdvanceCooldowns(e.Timestamp);

    /// <summary>
    /// Checks whether any in-flight cooldowns have naturally expired and fires
    /// <see cref="UpdateSpellUsableEvent"/> for each one, in chronological order.
    /// </summary>
    private void AdvanceCooldowns(int timestamp)
    {
        List<int>? expired = null;
        foreach (var (spellId, cd) in _cooldowns)
        {
            if (timestamp >= cd.ExpectedEnd)
            {
                expired ??= [];
                expired.Add(spellId);
            }
        }

        if (expired is null) return;

        expired.Sort((a, b) => _cooldowns[a].ExpectedEnd.CompareTo(_cooldowns[b].ExpectedEnd));

        foreach (var spellId in expired)
            EndCooldown(spellId, _cooldowns[spellId].ExpectedEnd);
    }

    /// <summary>
    /// Returns the current effective cooldown-rate multiplier for the given spell:
    /// the product of the global rate and any spell-specific rate. A value of 2.0
    /// means cooldowns elapse twice as fast.
    /// </summary>
    public double EffectiveRate(int spellId) =>
        _globalRateMultiplier * (_spellRateMultipliers.TryGetValue(spellId, out var r) ? r : 1.0);

    /// <summary>
    /// Multiplies the cooldown rate of <paramref name="spellId"/> by <paramref name="rateMultiplier"/>.
    /// Existing in-flight cooldowns for that spell are rescaled in place; future cooldowns
    /// start <paramref name="rateMultiplier"/>× shorter.
    /// </summary>
    public void ApplyCooldownRateChange(int spellId, double rateMultiplier, int? timestamp = null)
    {
        if (rateMultiplier <= 0 || rateMultiplier == 1.0) return;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        AdvanceCooldowns(ts);

        _spellRateMultipliers[spellId] =
            (_spellRateMultipliers.TryGetValue(spellId, out var r) ? r : 1.0) * rateMultiplier;

        if (_cooldowns.ContainsKey(spellId))
            HandleChangeRate(spellId, rateMultiplier, ts);
    }

    /// <summary>
    /// Multiplies the cooldown rate of every spell by <paramref name="rateMultiplier"/>.
    /// </summary>
    public void ApplyCooldownRateChangeToAll(double rateMultiplier, int? timestamp = null)
    {
        if (rateMultiplier <= 0 || rateMultiplier == 1.0) return;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        AdvanceCooldowns(ts);

        _globalRateMultiplier *= rateMultiplier;

        foreach (var spellId in _cooldowns.Keys.ToList())
            HandleChangeRate(spellId, rateMultiplier, ts);
    }

    /// <summary>Inverse of <see cref="ApplyCooldownRateChange"/>; pair with the same multiplier.</summary>
    public void RemoveCooldownRateChange(int spellId, double rateMultiplier, int? timestamp = null)
    {
        if (rateMultiplier <= 0 || rateMultiplier == 1.0) return;
        ApplyCooldownRateChange(spellId, 1.0 / rateMultiplier, timestamp);
    }

    /// <summary>Inverse of <see cref="ApplyCooldownRateChangeToAll"/>; pair with the same multiplier.</summary>
    public void RemoveCooldownRateChangeFromAll(double rateMultiplier, int? timestamp = null)
    {
        if (rateMultiplier <= 0 || rateMultiplier == 1.0) return;
        ApplyCooldownRateChangeToAll(1.0 / rateMultiplier, timestamp);
    }

    /// <summary>
    /// Rescales the in-flight cooldown for <paramref name="spellId"/>: remaining time is
    /// divided by <paramref name="rateChange"/>, total RechargeDuration is divided likewise.
    /// OverallStart and ChargeStart are preserved.
    /// </summary>
    private void HandleChangeRate(int spellId, double rateChange, int timestamp)
    {
        var cd = _cooldowns[spellId];
        var remaining = Math.Max(0, cd.ExpectedEnd - timestamp);
        var percentRemaining = cd.RechargeDuration == 0 ? 0 : (double)remaining / cd.RechargeDuration;
        var newRecharge = (int)Math.Round(cd.RechargeDuration / rateChange);
        var newRemaining = (int)Math.Round(newRecharge * percentRemaining);
        cd = cd with { RechargeDuration = newRecharge, ExpectedEnd = timestamp + newRemaining };
        _cooldowns[spellId] = cd;
        FabricateUpdate(UpdateSpellUsableType.ChangeCooldownRate, spellId, timestamp, cd);
    }

    /// <summary>
    /// Applies a cooldown-rate change scaled to the player's current haste:
    /// <c>rate = 1.0 + Haste.Current</c>. The lease automatically re-balances on every
    /// <see cref="ChangeHasteEvent"/> until released via <see cref="RemoveHasteScaledRateChange"/>.
    /// </summary>
    /// <param name="spellId">When non-null, scopes the rate change to a single spell; otherwise applies to all.</param>
    public CooldownRateLease ApplyHasteScaledRateChange(int? spellId = null, int? timestamp = null)
    {
        var rate = 1.0 + _haste.Current;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        ApplyForLease(spellId, rate, ts);
        var lease = new CooldownRateLease(_nextLeaseId++);
        _hasteScaledLeases[lease.Id] = new HasteScaledLease(spellId, rate);
        return lease;
    }

    /// <summary>Releases a haste-scaled lease, inverting whatever rate was last applied.</summary>
    public void RemoveHasteScaledRateChange(CooldownRateLease lease, int? timestamp = null)
    {
        if (!_hasteScaledLeases.Remove(lease.Id, out var info)) return;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        RemoveForLease(info.SpellId, info.LastAppliedRate, ts);
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        if (_hasteScaledLeases.Count == 0) return;
        var newRate = 1.0 + (e.NewHaste ?? 0.0);
        foreach (var leaseId in _hasteScaledLeases.Keys.ToList())
        {
            var info = _hasteScaledLeases[leaseId];
            if (info.LastAppliedRate == newRate) continue;
            var diff = newRate / info.LastAppliedRate;
            ApplyForLease(info.SpellId, diff, e.Timestamp);
            _hasteScaledLeases[leaseId] = info with { LastAppliedRate = newRate };
        }
    }

    private void ApplyForLease(int? spellId, double rate, int timestamp)
    {
        if (spellId is int id)
            ApplyCooldownRateChange(id, rate, timestamp);
        else
            ApplyCooldownRateChangeToAll(rate, timestamp);
    }

    private void RemoveForLease(int? spellId, double rate, int timestamp)
    {
        if (spellId is int id)
            RemoveCooldownRateChange(id, rate, timestamp);
        else
            RemoveCooldownRateChangeFromAll(rate, timestamp);
    }

    private void FabricateUpdate(UpdateSpellUsableType updateType, int spellId, int timestamp, CooldownInfo cd)
    {
        var ability = _abilities.GetAbility(spellId);

        Owner.EventEmitter.FabricateEvent(new UpdateSpellUsableEvent
        {
            Timestamp = timestamp,
            Ability = new Ability { Guid = spellId, Name = ability?.Name ?? string.Empty },
            UpdateType = updateType,
            IsOnCooldown = cd.ChargesAvailable < cd.MaxCharges,
            IsAvailable = cd.ChargesAvailable > 0,
            ChargesAvailable = cd.ChargesAvailable,
            MaxCharges = cd.MaxCharges,
            OverallStartTimestamp = cd.OverallStart,
            ChargeStartTimestamp = cd.ChargeStart,
            ExpectedRechargeTimestamp = cd.ExpectedEnd,
            ExpectedRechargeDuration = cd.RechargeDuration,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            SourceIsFriendly = true,
            TargetIsFriendly = true,
        });
    }

    private record struct CooldownInfo(
        int OverallStart,
        int ChargeStart,
        int ExpectedEnd,
        int RechargeDuration,
        int ChargesAvailable,
        int MaxCharges);

    private readonly record struct HasteScaledLease(int? SpellId, double LastAppliedRate);
}

/// <summary>
/// Opaque handle returned by <see cref="SpellUsable.ApplyHasteScaledRateChange"/>.
/// Pass back to <see cref="SpellUsable.RemoveHasteScaledRateChange"/> to release.
/// </summary>
public readonly record struct CooldownRateLease(int Id);

/// <summary>
/// A point-in-time record of a single player cast.
/// </summary>
public readonly record struct TrackedAbilityCast(
    int Timestamp,
    int Id,
    int TargetId);
