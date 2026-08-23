using FellowshipAnalyzer.Core.Common;
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

    /// <summary>Every player cast recorded during dispatch, in the order it occurred.</summary>
    public List<TrackedAbilityCast> Casts => _casts;

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public List<int> GetSpellsOnCooldown() => [.. _cooldowns.Keys];

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

    /// <summary>Whether <paramref name="spellId"/> has at least one charge available to cast right now.</summary>
    public bool IsAvailable(int spellId) => !_cooldowns.TryGetValue(spellId, out var cd) || cd.ChargesAvailable > 0;

    /// <summary>Whether any charge of <paramref name="spellId"/> is currently recharging.</summary>
    public bool IsOnCooldown(int spellId) => _cooldowns.ContainsKey(spellId);

    /// <summary>How many charges of <paramref name="spellId"/> can be cast right now.</summary>
    public int ChargesAvailable(int spellId) =>
        _cooldowns.TryGetValue(spellId, out var cd)
            ? cd.ChargesAvailable
            : _abilities.GetMaxCharges(spellId);

    /// <summary>
    /// Milliseconds until <paramref name="spellId"/>'s next charge recharges, evaluated at
    /// <paramref name="atTimestamp"/> (defaulting to the current dispatch time). Zero when a charge is
    /// already available.
    /// </summary>
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

    /// <summary>
    /// Consumes a charge of <paramref name="spellId"/>: starts a fresh recharge if none is running, spends an
    /// already-available charge if one exists, or, if every charge is spent, forces the current recharge to
    /// complete before beginning a new one, so the tracker stays in sync with an in-game cast it did not
    /// expect to be possible.
    /// </summary>
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

    /// <summary>
    /// Hands one charge of <paramref name="spellId"/> back without disturbing the recharge already
    /// running, as a refund proc does: the charge that was recovering keeps the progress it had made.
    /// <see cref="EndCooldown"/> is the wrong call for a refund, since it restarts the next charge's
    /// recharge from the moment it runs. A refund past the last charge on cooldown leaves nothing
    /// recharging, so the pending expiry is cancelled and the spell drops to fully available.
    /// </summary>
    /// <returns><c>true</c> when a charge was handed back, <c>false</c> when every charge was already available.</returns>
    public bool RefundCharge(int spellId, int? timestamp = null)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return false;

        var ts = timestamp ?? Owner.CurrentTimestamp;
        var eventTs = Owner.CurrentTimestamp;
        cd = cd with { ChargesAvailable = cd.ChargesAvailable + 1 };

        if (cd.ChargesAvailable < cd.MaxCharges)
        {
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.RestoreCharge, spellId, eventTs, cd);
            RefreshPendingEnd(spellId);
            return true;
        }

        if (cd.PendingEnd is not null)
        {
            Owner.EventEmitter.Cancel(cd.PendingEnd);
            cd = cd with { PendingEnd = null };
        }

        cd = cd with { ExpectedEnd = ts };
        FabricateUpdate(UpdateSpellUsableType.EndCooldown, spellId, eventTs, cd);
        _cooldowns.Remove(spellId);
        return true;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        _casts.Add(new TrackedAbilityCast(e.Timestamp, e.Ability.Id, e.TargetId));
        RecordCooldownDebugInfo(e);
        BeginCooldown(e.Ability.Id, e.Timestamp);
    }

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

    private double HasteRecovery(int spellId) =>
        _abilities.GetAbility(spellId)?.CooldownReducedByHaste == true ? _haste.Current : 0.0;

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        if (e.Pool != CooldownPool.CooldownAcceleration) return;
        RescaleChangedCooldowns(e.Timestamp);
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e) => RescaleChangedCooldowns(e.Timestamp);

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
/// The outcome of a <see cref="SpellUsable.ReduceCooldown"/> request, in milliseconds.
/// </summary>
/// <param name="Total">
/// The reduction the request produced, after Ability Cooldown Reduction scaling. A flat reduction is
/// shortened by ACR but is not divided by the cooldown-recovery pool.
/// </param>
/// <param name="Effective">
/// The amount of reduction that actually shortened a running cooldown
/// </param>
public readonly record struct CooldownReductionResult(int Total, int Effective)
{
    /// <summary>
    /// Creates a new <see cref="CooldownReductionResult"/> with no reduction applied.
    /// </summary>
    public CooldownReductionResult() : this(0, 0) { }

    /// <summary>Amount of reduction that was wasted, either because the spell was already off cooldown, or had less time remaining than the total reduction.</summary>
    public int Wasted => Total - Effective;

    /// <summary>Share (0-1) of <see cref="Total"/> that shortened a running cooldown. Zero when nothing was generated.</summary>
    public double Efficiency => Total > 0 ? (double)Effective / Total : 0;

    /// <summary>Adds two results together, so a run of requests can be accumulated into one.</summary>
    public static CooldownReductionResult operator +(CooldownReductionResult a, CooldownReductionResult b) =>
        new(a.Total + b.Total, a.Effective + b.Effective);
}

/// <summary>
/// A point-in-time record of a single player cast.
/// </summary>
public readonly record struct TrackedAbilityCast(
    int Timestamp,
    int Id,
    int TargetId);
