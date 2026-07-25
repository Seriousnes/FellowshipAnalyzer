using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks spell cooldown state during event dispatch, fabricating
/// <see cref="UpdateSpellUsableEvent"/> events when spells go on/off cooldown.
/// Also tracks all player casts (replacing the former TrackedStateModule).
/// </summary>
public sealed partial class SpellUsable(
    Lazy<Abilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private const int CooldownLagMargin = 150;

    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];

    public IReadOnlyList<TrackedAbilityCast> Casts => _casts;

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public IReadOnlyCollection<int> GetSpellsOnCooldown() => _cooldowns.Keys;

    /// <summary>
    /// Reduces the remaining cooldown of a spell by up to <paramref name="milliseconds"/>. The requested flat
    /// reduction (e.g. Rolling Flames) is scaled by that spell's Ability Cooldown Reduction before it is
    /// applied, so at 10% ACR a 1000ms request generates 900ms; it is not divided by the cooldown-recovery
    /// pool. For
    /// multi-charge spells each charge is restored in sequence as the reduction overflows into the next.
    /// </summary>
    /// <returns>
    /// How much reduction the request generated (after ACR scaling), and how much of that shortened a running
    /// cooldown. The two differ when the spell was already available, or had fewer milliseconds left than the
    /// generated reduction.
    /// </returns>
    public CooldownReductionResult ReduceCooldown(int spellId, int milliseconds, int? timestamp = null)
    {
        var generated = _statTracker.ScaleByCooldownReduction(_abilities.GetAbility(spellId), milliseconds);
        return new(generated, ApplyReduction(spellId, generated, timestamp));
    }

    private int ApplyReduction(int spellId, int milliseconds, int? timestamp)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd) || milliseconds <= 0)
            return 0;

        var remaining = Math.Max(0, cd.ExpectedEnd - (timestamp ?? Owner.CurrentTimestamp));

        if (milliseconds < remaining)
        {
            _cooldowns[spellId] = cd with { ExpectedEnd = cd.ExpectedEnd - milliseconds };
            RefreshPendingEnd(spellId);
            return milliseconds;
        }

        EndCooldown(spellId, timestamp ?? Owner.CurrentTimestamp);
        return remaining + ApplyReduction(spellId, milliseconds - remaining, timestamp);
    }

    public bool IsAvailable(int spellId) => !_cooldowns.TryGetValue(spellId, out var cd) || cd.ChargesAvailable > 0;

    public bool IsOnCooldown(int spellId) => _cooldowns.ContainsKey(spellId);

    public int ChargesAvailable(int spellId) =>
        _cooldowns.TryGetValue(spellId, out var cd)
            ? cd.ChargesAvailable
            : _abilities.GetMaxCharges(spellId);

    public int CooldownRemaining(int spellId, int? atTimestamp = null)
    {
        var ts = atTimestamp ?? Owner.CurrentTimestamp;
        return _cooldowns.TryGetValue(spellId, out var cd)
            ? Math.Max(0, cd.ExpectedEnd - ts)
            : 0;
    }

    /// <summary>
    /// The full recharge duration one charge of <paramref name="spellId"/> currently takes: its base
    /// cooldown after Ability Cooldown Reduction, divided by the current <see cref="EffectiveRate"/>.
    /// This is the value <see cref="BeginCooldown"/> assigns a fresh recharge, exposed for metrics that
    /// need the effective (haste/gear/recovery-accelerated) period rather than the raw curated cooldown.
    /// Returns 0 for a spell with no configured cooldown.
    /// </summary>
    public int RechargeDuration(int spellId)
    {
        var ability = _abilities.GetAbility(spellId);
        var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
        return baseDurationMs <= 0 ? 0 : (int)(_statTracker.ScaleByCooldownReduction(ability, baseDurationMs) / EffectiveRate(spellId));
    }

    public void BeginCooldown(int spellId, int? timestamp = null)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var ability = _abilities.GetAbility(spellId);
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var rate = EffectiveRate(spellId);
            var cdDuration = (int)(_statTracker.ScaleByCooldownReduction(ability, baseDurationMs) / rate);

            var maxCharges = _abilities.GetMaxCharges(spellId);
            cd = new CooldownInfo(
                OverallStart: ts,
                ChargeStart: ts,
                ExpectedEnd: ts + cdDuration,
                RechargeDuration: cdDuration,
                ChargesAvailable: maxCharges - 1,
                MaxCharges: maxCharges,
                Rate: rate,
                PendingEnd: null);
            _cooldowns[spellId] = cd;

            FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, ts, cd);
            RefreshPendingEnd(spellId);
        }
        else if (cd.ChargesAvailable > 0)
        {
            cd = cd with { ChargesAvailable = cd.ChargesAvailable - 1 };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.UseCharge, spellId, ts, cd);
            RefreshPendingEnd(spellId);
        }
        else
        {
            EndCooldown(spellId, ts);
            BeginCooldown(spellId, ts);
        }
    }

    /// <summary>
    /// Forces a charge (or, with <paramref name="restoreAllCharges"/>, all charges) back onto
    /// <paramref name="spellId"/> synchronously, e.g. from a reduction that completes a running recharge or
    /// a reset effect. The restore is applied to <see cref="_cooldowns"/> and its notification fabricated at
    /// the current dispatch time rather than scheduled, since it happens now rather than at a future natural
    /// expiry. Any pending natural-expiry end is cancelled; when charges remain on cooldown a fresh pending
    /// end is scheduled for the next charge.
    /// </summary>
    public void EndCooldown(int spellId, int? timestamp = null, bool restoreAllCharges = false)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return;

        var eventTs = Owner.CurrentTimestamp;

        if (cd.PendingEnd is not null)
        {
            Owner.EventEmitter.Cancel(cd.PendingEnd);
            cd = cd with { PendingEnd = null };
        }

        cd = restoreAllCharges
            ? cd with { ChargesAvailable = cd.MaxCharges, ExpectedEnd = ts }
            : cd with { ChargesAvailable = cd.ChargesAvailable + 1 };

        if (cd.ChargesAvailable >= cd.MaxCharges)
        {
            cd = cd with { ExpectedEnd = ts };
            FabricateUpdate(UpdateSpellUsableType.EndCooldown, spellId, eventTs, cd);
            _cooldowns.Remove(spellId);
        }
        else
        {
            var nextEnd = ts + cd.RechargeDuration;
            cd = cd with { ChargeStart = ts, ExpectedEnd = nextEnd };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.RestoreCharge, spellId, eventTs, cd);
            RefreshPendingEnd(spellId);
        }
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        _casts.Add(new TrackedAbilityCast(e.Timestamp, e.Ability.Id, e.TargetId));
        RecordCooldownDebugInfo(e);
        BeginCooldown(e.Ability.Id, e.Timestamp);
    }

    /// <summary>
    /// Records a debug annotation describing the cooldown state at a player cast, mirroring
    /// WoWAnalyzer's SpellUsable.recordCooldownDebugInfo. A cast that lands while the tracker
    /// believes the spell holds no charges and whose next recharge is still more than
    /// <see cref="CooldownLagMargin"/> ms away is flagged (casting with no charges is impossible
    /// in-game, so its configured cooldown or charge count is likely too slow); otherwise a spell
    /// that is not in the hero's spellbook is flagged.
    /// </summary>
    private void RecordCooldownDebugInfo(CastEvent e)
    {
        var ability = _abilities.GetAbility(e.Ability.Id);

        if (_cooldowns.TryGetValue(e.Ability.Id, out var cd)
            && cd.ChargesAvailable == 0
            && cd.ExpectedEnd - e.Timestamp > CooldownLagMargin)
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Danger,
                Summary: $"Used with no charges available: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: $"Tracker believed {e.Ability.Name} held 0/{cd.MaxCharges} charges with " +
                         $"{cd.ExpectedEnd - e.Timestamp}ms until the next recharge. Casting with no charges " +
                         $"is impossible in-game, so its configured cooldown or charge count is likely too slow.",
                Priority: 10));
        }
        else if (ability is null)
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Rust,
                Summary: $"Unconfigured spell: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: "This spell was cast by the player but is not in the hero's spellbook. " +
                         "Consider adding it to the Abilities module."));
        }
    }

    [On<FilterCooldownInfoEvent>(By = Actor.Player)]
    private void OnFilterCooldown(FilterCooldownInfoEvent e) =>
        BeginCooldown(e.Ability.Id, e.Timestamp);

    /// <summary>
    /// Applies the state transition for a natural-expiry end at the instant it is dispatched, and only for
    /// the event object currently held as this spell's pending end. Every other
    /// <see cref="UpdateSpellUsableEvent"/> (this module's own immediate notifications, other spells) is
    /// ignored, so the module never loops on its own output. The last charge coming back removes the
    /// cooldown; an earlier charge restores one and schedules a fresh pending end for the next charge's
    /// recharge. The just-dispatched end is now a frozen historical event and is never reused.
    /// </summary>
    [On<UpdateSpellUsableEvent>]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent e)
    {
        var spellId = e.Ability.Id;
        if (!_cooldowns.TryGetValue(spellId, out var cd) || !ReferenceEquals(cd.PendingEnd, e))
            return;

        if (e.UpdateType == UpdateSpellUsableType.EndCooldown)
        {
            _cooldowns.Remove(spellId);
            return;
        }

        var restoreTs = e.Timestamp;
        _cooldowns[spellId] = cd with
        {
            ChargesAvailable = cd.ChargesAvailable + 1,
            ChargeStart = restoreTs,
            ExpectedEnd = restoreTs + cd.RechargeDuration,
            PendingEnd = null,
        };
        RefreshPendingEnd(spellId);
    }

    /// <summary>
    /// Brings this spell's pending natural-expiry end into line with its current <see cref="CooldownInfo"/>.
    /// A spell with no pending end (a fresh recharge, or one whose end was just dispatched) gets a new event
    /// created and scheduled; one already pending is mutated in place and repositioned, never discarded, so a
    /// held reference stays valid across rescales and reductions.
    /// </summary>
    private void RefreshPendingEnd(int spellId)
    {
        var cd = _cooldowns[spellId];
        if (cd.PendingEnd is null)
        {
            var pending = CreatePendingEnd(spellId, cd);
            _cooldowns[spellId] = cd with { PendingEnd = pending };
            Owner.EventEmitter.Schedule(pending);
        }
        else
        {
            ApplyPendingEndState(cd.PendingEnd, cd);
            Owner.EventEmitter.Reschedule(cd.PendingEnd);
        }
    }

    private UpdateSpellUsableEvent CreatePendingEnd(int spellId, CooldownInfo cd)
    {
        var ability = _abilities.GetAbility(spellId);
        var e = new UpdateSpellUsableEvent
        {
            Ability = new Ability { FSLID = spellId, Name = ability?.Name ?? string.Empty },
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            SourceIsFriendly = true,
            TargetIsFriendly = true,
        };
        ApplyPendingEndState(e, cd);
        return e;
    }

    /// <summary>
    /// Writes the payload for the moment <paramref name="cd"/>'s in-flight recharge completes onto
    /// <paramref name="e"/>: an <see cref="UpdateSpellUsableType.EndCooldown"/> when the restored charge is
    /// the last one, otherwise a <see cref="UpdateSpellUsableType.RestoreCharge"/> whose charge-start and
    /// expected-recharge describe the next charge's window. The event timestamp is the true expiry instant.
    /// </summary>
    private static void ApplyPendingEndState(UpdateSpellUsableEvent e, CooldownInfo cd)
    {
        var endTs = cd.ExpectedEnd;
        var chargesAfter = cd.ChargesAvailable + 1;

        e.Timestamp = endTs;
        e.OverallStartTimestamp = cd.OverallStart;
        e.ExpectedRechargeDuration = cd.RechargeDuration;
        e.MaxCharges = cd.MaxCharges;
        e.ChargesAvailable = chargesAfter;

        if (chargesAfter >= cd.MaxCharges)
        {
            e.UpdateType = UpdateSpellUsableType.EndCooldown;
            e.IsOnCooldown = false;
            e.IsAvailable = true;
            e.ChargeStartTimestamp = cd.ChargeStart;
            e.ExpectedRechargeTimestamp = endTs;
        }
        else
        {
            e.UpdateType = UpdateSpellUsableType.RestoreCharge;
            e.IsOnCooldown = true;
            e.IsAvailable = true;
            e.ChargeStartTimestamp = endTs;
            e.ExpectedRechargeTimestamp = endTs + cd.RechargeDuration;
        }
    }

    /// <summary>
    /// The cooldown-speed multiplier for <paramref name="spellId"/>: <c>1 + CDA</c>, where the Cooldown
    /// Acceleration pool sums the haste term (the player's haste when the ability is flagged
    /// <c>CooldownReducedByHaste</c>, else 0) and the pool <see cref="StatTracker"/> tracks for this ability:
    /// the gear-derived seed (today a legendary's unscoped Strand of Eternity) plus tracked runtime modifiers
    /// such as Chronoshift, with scoped entries contributing only to the abilities they match. Recovery and
    /// acceleration are one mechanic fed by a single additive pool, so each source contributes a term rather
    /// than an independent factor; a value of 9.0 means the spell's cooldown elapses 9× faster. Unlike
    /// Ability Cooldown Reduction, which <see cref="ReduceCooldown"/> and <see cref="BeginCooldown"/>
    /// snapshot at cast, CDA is dynamic: a change to any term rescales the affected in-flight cooldowns.
    /// </summary>
    public double EffectiveRate(int spellId) =>
        1.0 + HasteRecovery(spellId)
            + _statTracker.CurrentCooldownAcceleration(_abilities.GetAbility(spellId));

    /// <summary>
    /// Haste's contribution to <paramref name="spellId"/>'s recovery pool: the player's current haste
    /// when the ability's cooldown is reduced by haste, otherwise 0.
    /// </summary>
    private double HasteRecovery(int spellId) =>
        _abilities.GetAbility(spellId)?.CooldownReducedByHaste == true ? _haste.Current : 0.0;

    /// <summary>
    /// Rescales in-flight cooldowns when the tracked Cooldown Acceleration pool changes, the CDA
    /// counterpart of <see cref="OnChangeHaste"/>. Each in-flight cooldown is compared against its own
    /// current <see cref="EffectiveRate"/>, which differs per spell because haste and scoped modifiers
    /// share the pool; a cooldown whose ability the change does not cover keeps its rate and is left
    /// alone. Comparing against the rate stored on the cooldown makes several pool mutations batched
    /// under one event converge on the final rate rather than compounding. Ability Cooldown Reduction
    /// changes are ignored: ACR is snapshot semantics and never rescales a cooldown in flight.
    /// </summary>
    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        if (e.Pool != CooldownPool.CooldownAcceleration) return;
        RescaleChangedCooldowns(e.Timestamp);
    }

    /// <summary>
    /// Rescales in-flight haste-reduced cooldowns when the player's haste changes, so their remaining
    /// time reflects the new recovery rate for the rest of the cooldown. Haste is a term on the same
    /// pool the tracked acceleration modifiers feed, so this shares the rate-comparison sweep with
    /// <see cref="OnChangeCooldownModifier"/>: only cooldowns whose <see cref="EffectiveRate"/> actually
    /// moved (haste-flagged abilities, for a haste change) are touched.
    /// </summary>
    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e) => RescaleChangedCooldowns(e.Timestamp);

    /// <summary>
    /// Sweeps every in-flight cooldown and rescales those whose current <see cref="EffectiveRate"/>
    /// differs from the rate the cooldown was last scaled at. Any cooldown that had genuinely expired
    /// already fired its earlier-scheduled end and left <see cref="_cooldowns"/>, so those still present
    /// are all in flight.
    /// </summary>
    private void RescaleChangedCooldowns(int timestamp)
    {
        foreach (var spellId in _cooldowns.Keys.ToList())
        {
            if (!_cooldowns.TryGetValue(spellId, out var cd)) continue;

            var newRate = EffectiveRate(spellId);
            if (newRate <= 0 || newRate == cd.Rate) continue;

            HandleChangeRate(spellId, newRate, timestamp);
        }
    }

    /// <summary>
    /// Rescales the in-flight cooldown for <paramref name="spellId"/> to <paramref name="newRate"/>:
    /// remaining time and total RechargeDuration are divided by the change relative to the rate the
    /// cooldown was last scaled at. OverallStart and ChargeStart are preserved.
    /// </summary>
    private void HandleChangeRate(int spellId, double newRate, int timestamp)
    {
        var cd = _cooldowns[spellId];
        var rateChange = newRate / cd.Rate;
        var remaining = Math.Max(0, cd.ExpectedEnd - timestamp);
        var percentRemaining = cd.RechargeDuration == 0 ? 0 : (double)remaining / cd.RechargeDuration;
        var newRecharge = (int)Math.Round(cd.RechargeDuration / rateChange);
        var newRemaining = (int)Math.Round(newRecharge * percentRemaining);
        cd = cd with { RechargeDuration = newRecharge, ExpectedEnd = timestamp + newRemaining, Rate = newRate };
        _cooldowns[spellId] = cd;
        FabricateUpdate(UpdateSpellUsableType.ChangeCooldownRate, spellId, timestamp, cd);
        RefreshPendingEnd(spellId);
    }

    private void FabricateUpdate(UpdateSpellUsableType updateType, int spellId, int timestamp, CooldownInfo cd)
    {
        var ability = _abilities.GetAbility(spellId);

        Owner.EventEmitter.FabricateEvent(new UpdateSpellUsableEvent
        {
            Timestamp = timestamp,
            Ability = new Ability { FSLID = spellId, Name = ability?.Name ?? string.Empty },
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

    /// <param name="Rate">The <see cref="EffectiveRate"/> the cooldown was last scaled at, so a pool
    /// change can be applied as the ratio between the new rate and this one.</param>
    /// <param name="PendingEnd">The scheduled natural-expiry event for the in-flight recharge, held so a
    /// rescale or reduction can mutate and reposition it while it is still in the future, and so its
    /// dispatch can be recognised by reference. Null between constructing a fresh cooldown and scheduling
    /// its end.</param>
    private record struct CooldownInfo(
        int OverallStart,
        int ChargeStart,
        int ExpectedEnd,
        int RechargeDuration,
        int ChargesAvailable,
        int MaxCharges,
        double Rate,
        UpdateSpellUsableEvent? PendingEnd);
}

/// <summary>
/// The outcome of a <see cref="SpellUsable.ReduceCooldown"/> request.
/// </summary>
/// <param name="GeneratedMs">
/// Reduction the request generated, in milliseconds. This is the requested amount after Ability Cooldown
/// Reduction scaling: flat reductions are shortened by ACR but are not divided by the cooldown-recovery pool.
/// </param>
/// <param name="AppliedMs">How much of <paramref name="GeneratedMs"/> shortened a running cooldown.</param>
public readonly record struct CooldownReductionResult(int GeneratedMs, int AppliedMs)
{
    /// <summary>Reduction generated while the spell was already off cooldown, in milliseconds.</summary>
    public int WastedMs => GeneratedMs - AppliedMs;
}

/// <summary>
/// A point-in-time record of a single player cast.
/// </summary>
public readonly record struct TrackedAbilityCast(
    int Timestamp,
    int Id,
    int TargetId);
