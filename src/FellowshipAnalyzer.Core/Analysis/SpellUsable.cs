using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks spell cooldown state during event dispatch, fabricating
/// <see cref="UpdateSpellUsableEvent"/> events when spells go on/off cooldown.
/// Also tracks all player casts (replacing the former TrackedStateModule).
/// </summary>
/// <remarks>
/// Each ability has one <see cref="CooldownState"/> for the whole dungeon, keyed by its primary spell id, so a
/// cast under an additional spell id spends a charge of the primary spell's state. The log's casts are the
/// authority: a cast logged while the state has no charge returns the charge recharging at the cast and adds
/// a <see cref="CooldownDrift"/> to <see cref="Drifts"/>.
/// </remarks>
public partial class SpellUsable(
    Lazy<Abilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private const int CooldownLagMargin = 150;
    private const double RechargeTolerance = 1e-6;

    private readonly Dictionary<int, CooldownState> _cooldowns = [];
    private readonly Dictionary<int, UpdateSpellUsableEvent> _pending = [];
    private readonly Dictionary<int, List<ChargeTimer>> _timers = [];
    private readonly List<CooldownDrift> _drifts = [];
    private readonly List<TrackedAbilityCast> _casts = [];
    private Dictionary<int, int>? _deferredStarts;

    /// <summary>Every player cast during dispatch, in the order it occurred.</summary>
    public List<TrackedAbilityCast> Casts => _casts;

    /// <summary>
    /// Every cast logged while the ability's state had no charge, in the order it occurred. The state
    /// returned the charge recharging at the cast.
    /// </summary>
    public IReadOnlyList<CooldownDrift> Drifts => _drifts;

    /// <summary>
    /// The <see cref="CooldownState"/> of <paramref name="spellId"/>'s ability, or <c>null</c> before the
    /// ability's first cooldown starts. An ability declaring <see cref="SpellbookAbility.IndependentCharges"/>
    /// has one single-charge state per spent charge, and this is the state of the earliest, or <c>null</c>
    /// with none recharging.
    /// </summary>
    public CooldownState? StateOf(int spellId)
    {
        var key = KeyOf(spellId);
        return _timers.TryGetValue(key, out var timers)
            ? timers.FirstOrDefault()?.State
            : _cooldowns.GetValueOrDefault(key);
    }

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public virtual List<int> GetSpellsOnCooldown() =>
    [
        .. _cooldowns.Where(static cooldown => cooldown.Value.Recharging).Select(static cooldown => cooldown.Key),
        .. _timers.Where(static timers => timers.Value.Count > 0).Select(static timers => timers.Key),
    ];

    /// <summary>
    /// Reduces the remaining cooldown of a spell by <paramref name="milliseconds"/> of cooldown progress
    /// (e.g. Rolling Flames). Neither Ability Cooldown Reduction nor the recovery rate scales the request: the
    /// progress left shortens by exactly <paramref name="milliseconds"/>, and the time left by
    /// <c>milliseconds / EffectiveRate</c>. For multi-charge spells each charge is restored in sequence as the
    /// reduction overflows into the next. An ability declaring <see cref="SpellbookAbility.IndependentCharges"/>
    /// takes the reduction on its earliest timer, and a reduction past that timer's end completes it and
    /// shortens the next.
    /// </summary>
    /// <returns>
    /// The requested cooldown progress, and how much of it shortened a running cooldown, both in
    /// milliseconds of cooldown progress. The two differ when the spell was already available, held, or had
    /// less progress left than the request.
    /// </returns>
    public virtual CooldownReductionResult ReduceCooldown(int spellId, int milliseconds, int? timestamp = null)
    {
        if (milliseconds <= 0)
            return new(0, 0);

        var key = KeyOf(spellId);
        var ts = timestamp ?? Owner.CurrentTimestamp;
        var applied = _timers.ContainsKey(key) ? ReduceTimers(key, milliseconds, ts) : ReduceState(key, milliseconds, ts);
        return new(milliseconds, (int)Math.Round(applied, MidpointRounding.AwayFromZero));
    }

    private double ReduceState(int key, int milliseconds, int ts)
    {
        if (Advanced(key, ts) is not { } state)
            return 0;

        var before = state.Charges;
        var applied = state.Reduce(ts, milliseconds);
        FabricateReturns(key, state, ts, 0, state.Charges - before);
        SyncPending(key);
        return applied;
    }

    private double ReduceTimers(int key, double milliseconds, int ts)
    {
        var timers = _timers[key];
        AdvanceTimers(key, ts);
        var left = milliseconds;
        var applied = 0.0;
        while (left > 0 && timers.Count > 0)
        {
            var timer = timers[0];
            var taken = timer.State.Reduce(ts, left);
            applied += taken;
            left -= taken;
            if (timer.State.Recharging)
                break;

            CompleteTimers(key, timers, 0, 1, ts);
        }

        SyncTimers(key);
        return applied;
    }

    /// <summary>Whether <paramref name="spellId"/> has at least one charge available to cast right now.</summary>
    public virtual bool IsAvailable(int spellId) => ChargesAvailable(spellId) > 0;

    /// <summary>Whether any charge of <paramref name="spellId"/> is currently recharging.</summary>
    public virtual bool IsOnCooldown(int spellId)
    {
        var key = KeyOf(spellId);
        return _timers.TryGetValue(key, out var timers)
            ? timers.Count > 0
            : _cooldowns.TryGetValue(key, out var state) && state.Recharging;
    }

    /// <summary>How many charges of <paramref name="spellId"/> can be cast right now.</summary>
    public virtual int ChargesAvailable(int spellId)
    {
        var key = KeyOf(spellId);
        if (_timers.TryGetValue(key, out var timers))
            return _abilities.GetMaxCharges(key) - timers.Count;

        return _cooldowns.TryGetValue(key, out var state) ? state.Charges : _abilities.GetMaxCharges(key);
    }

    /// <summary>
    /// Milliseconds until <paramref name="spellId"/>'s next charge recharges, evaluated at
    /// <paramref name="atTimestamp"/> (defaulting to the current dispatch time). Zero when a charge is
    /// already available. A held cooldown reports <see cref="RechargeDuration"/>. The member reads the state
    /// and changes nothing.
    /// </summary>
    public virtual int CooldownRemaining(int spellId, int? atTimestamp = null)
    {
        var key = KeyOf(spellId);
        var at = atTimestamp ?? Owner.CurrentTimestamp;
        if (StateOf(key) is not { Recharging: true } state)
            return 0;

        if (state.Held)
            return RechargeDuration(key);

        return state.NextChargeAt is { } next ? Math.Max(0, next - at) : RechargeDuration(key);
    }

    /// <summary>
    /// The full recharge duration one charge of <paramref name="spellId"/> currently takes: its base
    /// cooldown after Ability Cooldown Reduction and Haste on its duration, divided by the current
    /// <see cref="EffectiveRate"/>. This is the value <see cref="BeginCooldown"/> assigns a fresh recharge,
    /// exposed for metrics that need the effective (haste/gear/recovery-accelerated) period rather than the
    /// raw curated cooldown. Returns 0 for a spell with no configured cooldown.
    /// </summary>
    public virtual int RechargeDuration(int spellId) =>
        (int)Math.Round(ExactRechargeDuration(spellId), MidpointRounding.AwayFromZero);

    private double ExactRechargeDuration(int spellId)
    {
        var key = KeyOf(spellId);
        var baseMs = _abilities.GetExpectedCooldown(key) * 1000;
        return baseMs <= 0 ? 0 : StartLength(_abilities.GetAbility(key), baseMs) / EffectiveRate(key);
    }

    /// <summary>
    /// Consumes a charge of <paramref name="spellId"/>. A spend from every charge sets the cooldown
    /// milliseconds of the recharge, and every later charge of the chain recharges over that length. A cast
    /// logged while every charge is spent returns the charge recharging at the cast, so the tracker stays in
    /// sync with an in-game cast it did not expect to be possible, and adds a <see cref="CooldownDrift"/>. An
    /// ability declaring <see cref="SpellbookAbility.CooldownStartsWhenBuffEnds"/> consumes its charge and
    /// holds, with nothing recharging until that buff leaves the player. An ability declaring
    /// <see cref="SpellbookAbility.IndependentCharges"/> starts a timer of its own for the charge this cast
    /// spent, alongside any timer already running.
    /// </summary>
    public virtual void BeginCooldown(int spellId, int? timestamp = null)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        var key = KeyOf(spellId);
        var baseMs = _abilities.GetExpectedCooldown(key) * 1000;
        if (baseMs <= 0)
            return;

        var ability = _abilities.GetAbility(key);
        if (ability?.IndependentCharges == true)
        {
            BeginIndependentCooldown(key, ability, baseMs, ts);
            return;
        }

        if (!_cooldowns.ContainsKey(key))
            _cooldowns[key] = new CooldownState(_abilities.GetMaxCharges(key));

        var state = Advanced(key, ts)!;
        if (state.Charges == 0)
        {
            _drifts.Add(new CooldownDrift(key, ts, Lateness(key, state, ts)));
            state.CompleteCharge(ts);
            FabricateUpdate(
                state.Recharging ? UpdateSpellUsableType.RestoreCharge : UpdateSpellUsableType.EndCooldown,
                key,
                ts,
                Describe(key, state, ts));
        }

        var wasFull = state.Charges == state.MaxCharges;
        state.Spend(ts, StartLength(ability, baseMs), EffectiveRate(key), hold: ability?.CooldownStartsWhenBuffEnds is not null);
        FabricateUpdate(
            wasFull ? UpdateSpellUsableType.BeginCooldown : UpdateSpellUsableType.UseCharge,
            key,
            ts,
            Describe(key, state, ts));
        SyncPending(key);
    }

    /// <summary>
    /// Forces a charge (or, with <paramref name="restoreAllCharges"/>, all charges) back onto
    /// <paramref name="spellId"/> synchronously, e.g. from a reduction that completes a running recharge or
    /// a reset effect. The restore is applied to the state and its notification fabricated at the current
    /// dispatch time rather than scheduled, since it happens now rather than at a future natural expiry. A
    /// single restored charge starts a full recharge of the next charge when charges remain on cooldown. An
    /// ability declaring <see cref="SpellbookAbility.IndependentCharges"/> completes its earliest timer, or
    /// every timer with <paramref name="restoreAllCharges"/>, and the timers left keep running.
    /// </summary>
    public virtual void EndCooldown(int spellId, int? timestamp = null, bool restoreAllCharges = false)
    {
        var key = KeyOf(spellId);
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (_timers.TryGetValue(key, out var timers))
        {
            AdvanceTimers(key, ts);
            if (timers.Count > 0)
            {
                CompleteTimers(key, timers, 0, restoreAllCharges ? timers.Count : 1, Owner.CurrentTimestamp);
                SyncTimers(key);
            }

            return;
        }

        if (Advanced(key, ts) is not { Recharging: true } state)
            return;

        if (restoreAllCharges)
            state.ResetAll(ts);
        else
            state.CompleteCharge(ts);

        var eventTs = Owner.CurrentTimestamp;
        FabricateUpdate(
            state.Recharging ? UpdateSpellUsableType.RestoreCharge : UpdateSpellUsableType.EndCooldown,
            key,
            eventTs,
            Describe(key, state, eventTs));
        SyncPending(key);
    }

    /// <summary>
    /// Hands one charge of <paramref name="spellId"/> back without disturbing the recharge already
    /// running, as a refund proc does: the charge that was recovering keeps the progress it had made.
    /// <see cref="EndCooldown"/> is the wrong call for a refund, since it restarts the next charge's
    /// recharge from the moment it runs. A refund past the last charge on cooldown leaves nothing
    /// recharging. An ability declaring <see cref="SpellbookAbility.IndependentCharges"/> drops its latest
    /// timer, and the timers left keep the progress they had made.
    /// </summary>
    /// <returns><c>true</c> when a charge was handed back, <c>false</c> when every charge was already available.</returns>
    public virtual bool RefundCharge(int spellId, int? timestamp = null)
    {
        var key = KeyOf(spellId);
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (_timers.TryGetValue(key, out var timers))
        {
            AdvanceTimers(key, ts);
            if (timers.Count == 0)
                return false;

            CompleteTimers(key, timers, timers.Count - 1, 1, Owner.CurrentTimestamp);
            SyncTimers(key);
            return true;
        }

        if (Advanced(key, ts) is not { } state || !state.GrantCharge(ts))
            return false;

        var eventTs = Owner.CurrentTimestamp;
        FabricateUpdate(
            state.Recharging ? UpdateSpellUsableType.RestoreCharge : UpdateSpellUsableType.EndCooldown,
            key,
            eventTs,
            Describe(key, state, eventTs));
        SyncPending(key);
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
        var key = KeyOf(e.Ability.Id);

        if (LateWithNoCharge(key, e.Timestamp) is { } late && late > CooldownLagMargin)
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Danger,
                Summary: $"Used with no charges available: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: $"Tracker believed {e.Ability.Name} held 0/{_abilities.GetMaxCharges(key)} charges with " +
                         $"{late}ms until the next recharge. Casting with no charges " +
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

    private int? LateWithNoCharge(int key, int ts)
    {
        if (_timers.TryGetValue(key, out var timers))
        {
            AdvanceTimers(key, ts);
            return timers.Count >= _abilities.GetMaxCharges(key) ? Lateness(key, timers[0].State, ts) : null;
        }

        return Advanced(key, ts) is { Charges: 0 } state ? Lateness(key, state, ts) : null;
    }

    private int Lateness(int key, CooldownState state, int ts) =>
        !state.Held && state.NextChargeAt is { } next ? Math.Max(0, next - ts) : RechargeDuration(key);

    [On<FilterCooldownInfoEvent>(By = Actor.Player)]
    private void OnFilterCooldown(FilterCooldownInfoEvent e) =>
        BeginCooldown(e.Ability.Id, e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnDeferredCooldownBuffRemoved(RemoveBuffEvent e)
    {
        if (!DeferredStarts.TryGetValue(e.Ability.Id, out var key))
            return;

        if (Advanced(key, e.Timestamp) is not { Held: true } state)
            return;

        var ability = _abilities.GetAbility(key);
        state.Release(e.Timestamp, StartLength(ability, _abilities.GetExpectedCooldown(key) * 1000), EffectiveRate(key));
        FabricateUpdate(UpdateSpellUsableType.BeginCooldown, key, e.Timestamp, Describe(key, state, e.Timestamp));
        SyncPending(key);
    }

    private Dictionary<int, int> DeferredStarts => _deferredStarts ??= BuildDeferredStarts();

    private Dictionary<int, int> BuildDeferredStarts()
    {
        var map = new Dictionary<int, int>();
        foreach (var ability in _abilities.GetAbilities())
            if (ability.CooldownStartsWhenBuffEnds is { } buff)
                map[buff.FSLID] = ability.PrimarySpell.FSLID;

        return map;
    }

    [On<UpdateSpellUsableEvent>]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent e)
    {
        var key = e.Ability.Id;
        if (_timers.TryGetValue(key, out var timers))
        {
            if (timers.Find(timer => ReferenceEquals(timer.Pending, e)) is { } returned)
            {
                returned.State.Advance(e.Timestamp);
                timers.Remove(returned);
                SyncTimers(key);
            }

            return;
        }

        if (!_pending.TryGetValue(key, out var pending) || !ReferenceEquals(pending, e))
            return;

        _pending.Remove(key);
        var state = _cooldowns[key];
        FabricateReturns(key, state, e.Timestamp, 1, state.Advance(e.Timestamp));
        SyncPending(key);
    }

    private CooldownState? Advanced(int key, int ts)
    {
        if (!_cooldowns.TryGetValue(key, out var state))
            return null;

        var returned = state.Advance(ts);
        if (returned > 0)
        {
            DropPending(key);
            FabricateReturns(key, state, ts, 0, returned);
            SyncPending(key);
        }

        return state;
    }

    private void FabricateReturns(int key, CooldownState state, int ts, int from, int returned)
    {
        for (var i = from; i < returned; i++)
        {
            var charges = state.Charges - (returned - 1 - i);
            FabricateUpdate(
                charges >= state.MaxCharges ? UpdateSpellUsableType.EndCooldown : UpdateSpellUsableType.RestoreCharge,
                key,
                ts,
                Describe(key, state, ts, charges));
        }
    }

    private void DropPending(int key)
    {
        if (_pending.Remove(key, out var pending))
            Owner.EventEmitter.Cancel(pending);
    }

    private void SyncPending(int key)
    {
        var state = _cooldowns[key];
        if (state.NextChargeAt is not { } at)
        {
            DropPending(key);
            return;
        }

        if (_pending.TryGetValue(key, out var pending))
        {
            ApplyPendingState(key, pending, state, at);
            Owner.EventEmitter.Reschedule(pending);
            return;
        }

        pending = NewPendingUpdate(key);
        ApplyPendingState(key, pending, state, at);
        _pending[key] = pending;
        Owner.EventEmitter.Schedule(pending);
    }

    private UpdateSpellUsableEvent NewPendingUpdate(int spellId)
    {
        var ability = _abilities.GetAbility(spellId);
        return new UpdateSpellUsableEvent
        {
            Ability = new Ability { FSLID = spellId, Name = ability?.Name ?? string.Empty },
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            SourceIsFriendly = true,
            TargetIsFriendly = true,
        };
    }

    private void ApplyPendingState(int key, UpdateSpellUsableEvent e, CooldownState state, int at)
    {
        var chargesAfter = state.Charges + 1;
        var full = chargesAfter >= state.MaxCharges;

        e.Timestamp = at;
        e.OverallStartTimestamp = state.OverallStart;
        e.MaxCharges = state.MaxCharges;
        e.ChargesAvailable = chargesAfter;
        e.IsAvailable = true;
        e.IsOnCooldown = !full;
        e.UpdateType = full ? UpdateSpellUsableType.EndCooldown : UpdateSpellUsableType.RestoreCharge;
        e.ExpectedRechargeDuration = RechargeLengthMilliseconds(key, state);

        if (full)
        {
            e.ChargeStartTimestamp = state.ChargeStart;
            e.ExpectedRechargeTimestamp = at;
            return;
        }

        e.ChargeStartTimestamp = at;
        e.ExpectedRechargeTimestamp = (int)Math.Ceiling(state.Timestamp + (state.ProgressLeft + state.RechargeLength) / state.Rate - RechargeTolerance);
    }

    private int RechargeLengthMilliseconds(int key, CooldownState state) =>
        state.Rate > 0
            ? (int)Math.Round(state.RechargeLength / state.Rate, MidpointRounding.AwayFromZero)
            : RechargeDuration(key);

    private readonly record struct Snapshot(int Charges, int MaxCharges, int OverallStart, int ChargeStart, int NextAt, int Duration);

    private Snapshot Describe(int key, CooldownState state, int ts, int? charges = null, int? maxCharges = null) => new(
        charges ?? state.Charges,
        maxCharges ?? state.MaxCharges,
        state.OverallStart,
        state.ChargeStart,
        state.NextChargeAt ?? ts,
        RechargeLengthMilliseconds(key, state));

    private void FabricateUpdate(UpdateSpellUsableType updateType, int key, int timestamp, Snapshot snapshot)
    {
        var ability = _abilities.GetAbility(key);

        Owner.EventEmitter.FabricateEvent(new UpdateSpellUsableEvent
        {
            Timestamp = timestamp,
            Ability = new Ability { FSLID = key, Name = ability?.Name ?? string.Empty },
            UpdateType = updateType,
            IsOnCooldown = snapshot.Charges < snapshot.MaxCharges,
            IsAvailable = snapshot.Charges > 0,
            ChargesAvailable = snapshot.Charges,
            MaxCharges = snapshot.MaxCharges,
            OverallStartTimestamp = snapshot.OverallStart,
            ChargeStartTimestamp = snapshot.ChargeStart,
            ExpectedRechargeTimestamp = snapshot.NextAt,
            ExpectedRechargeDuration = snapshot.Duration,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            SourceIsFriendly = true,
            TargetIsFriendly = true,
        });
    }

    private int KeyOf(int spellId) => _abilities.GetAbility(spellId)?.PrimarySpell.FSLID.Value ?? spellId;

    /// <summary>
    /// The recovery rate of <paramref name="spellId"/>'s cooldown, the progress it makes per millisecond:
    /// the sum of three terms. The first is <see cref="StatTracker.CurrentCooldownRecovery"/> when the
    /// ability's <see cref="SpellbookAbility.CooldownScaling"/> has
    /// <see cref="CooldownScaling.TimeRateCooldownRecovery"/>, and 1 otherwise. The second is
    /// <see cref="StatTracker.CurrentCooldownAcceleration"/> for the ability. The third is the player's Haste
    /// when Haste enters the ability's time rate, else 0, so Haste adds outside the Cooldown Recovery
    /// attribute. Unlike Ability Cooldown Reduction, which <see cref="BeginCooldown"/> fixes at cast, the rate
    /// is dynamic: a change to any term changes the recovery rate of the affected in-flight cooldowns.
    /// </summary>
    public virtual double EffectiveRate(int spellId)
    {
        var ability = _abilities.GetAbility(spellId);
        var recovery = ability?.CooldownScaling is { TimeRateCooldownRecovery: true } ? _statTracker.CurrentCooldownRecovery : 1.0;
        var haste = ability?.CooldownHaste == CooldownHasteScaling.TimeRate ? _haste.Current : 0.0;
        return recovery + _statTracker.CurrentCooldownAcceleration(ability) + haste;
    }

    /// <summary>
    /// The cooldown milliseconds a recharge starting now needs: the base after Ability Cooldown Reduction,
    /// where the ability's duration scales off it, divided by <c>1 + haste</c> when Haste scales the duration.
    /// The value has no rounding.
    /// </summary>
    private double StartLength(SpellbookAbility? ability, double baseMs)
    {
        var reduction = ability?.CooldownScaling is { DurationCooldownReduction: true }
            ? _statTracker.CurrentAbilityCooldownReduction(ability)
            : 0.0;
        var haste = ability?.CooldownHaste == CooldownHasteScaling.Duration ? 1.0 + _haste.Current : 1.0;
        return baseMs * (1.0 - reduction) / haste;
    }

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        ChangeRates(e.Timestamp);
        SampleRechargeDurations(e.Timestamp);
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        ChangeRates(e.Timestamp);
        SampleRechargeDurations(e.Timestamp);
    }

    [On<DungeonStartEvent>]
    private void OnDungeonStartSample(DungeonStartEvent e) => SampleRechargeDurations(e.Timestamp);

    private void ChangeRates(int ts)
    {
        foreach (var (key, state) in _cooldowns.ToList())
        {
            var rate = EffectiveRate(key);
            var earlier = state.Rate;
            var returned = state.SetRate(ts, rate);
            if (returned > 0)
            {
                DropPending(key);
                FabricateReturns(key, state, ts, 0, returned);
            }

            if (state.Recharging && !state.Held && rate != earlier)
                FabricateUpdate(UpdateSpellUsableType.ChangeCooldownRate, key, ts, Describe(key, state, ts));

            SyncPending(key);
        }

        foreach (var (key, timers) in _timers.ToList())
        {
            if (timers.Count == 0)
                continue;

            var rate = EffectiveRate(key);
            var changed = rate != timers[0].State.Rate;
            List<ChargeTimer> returned = [];
            foreach (var timer in timers)
            {
                if (timer.State.SetRate(ts, rate) > 0)
                    returned.Add(timer);
            }

            foreach (var timer in returned)
            {
                timers.Remove(timer);
                Owner.EventEmitter.Cancel(timer.Pending);
                FabricateTimerRemoval(key, timers, timer, ts);
            }

            SyncTimers(key);
            if (changed && timers.Count > 0)
                FabricateUpdate(UpdateSpellUsableType.ChangeCooldownRate, key, ts, TimerSnapshot(key, timers, ts));
        }
    }

    /// <summary>
    /// The time-weighted mean of <see cref="RechargeDuration"/> for <paramref name="spellId"/> across
    /// <paramref name="start"/> to <paramref name="end"/>, from the recharge durations in force while
    /// the dungeon ran. Reads the current <see cref="RechargeDuration"/> when the window holds none.
    /// </summary>
    public int RechargeDurationDuring(int spellId, int start, int end)
    {
        var key = KeyOf(spellId);
        if (end <= start || !_rechargeHistory.TryGetValue(key, out var history) || history.Count == 0)
            return RechargeDuration(spellId);

        double weighted = 0;
        var duration = history[0].Duration;
        var at = start;
        foreach (var (timestamp, value) in history)
        {
            if (timestamp <= start)
            {
                duration = value;
                continue;
            }

            if (timestamp >= end) break;

            weighted += duration * (timestamp - at);
            at = timestamp;
            duration = value;
        }

        weighted += duration * (end - at);
        return (int)Math.Round(weighted / (end - start));
    }

    /// <summary>
    /// The time-weighted mean of <see cref="RechargeDuration"/> for <paramref name="spellId"/> across
    /// <paramref name="pull"/>, so a pull analyzer reads the recharge its own pull's stats gave.
    /// </summary>
    public int RechargeDurationDuring(int spellId, PullStartEvent pull) =>
        RechargeDurationDuring(spellId, pull.StartTime, pull.EndTime);

    /// <summary>
    /// The recharges of <paramref name="spellId"/> that fit across <paramref name="start"/> to
    /// <paramref name="end"/>: each stretch of the window divided by the recharge duration in force
    /// across it, summed.
    /// </summary>
    public double RechargesDuring(int spellId, int start, int end)
    {
        if (end <= start)
            return 0;

        var key = KeyOf(spellId);
        if (!_rechargeHistory.TryGetValue(key, out var history) || history.Count == 0)
        {
            var current = ExactRechargeDuration(spellId);
            return current > 0 ? (end - start) / current : 0;
        }

        double recharges = 0;
        var duration = history[0].Duration;
        var at = start;
        foreach (var (timestamp, value) in history)
        {
            if (timestamp <= start)
            {
                duration = value;
                continue;
            }

            if (timestamp >= end) break;

            recharges += (timestamp - at) / duration;
            at = timestamp;
            duration = value;
        }

        recharges += (end - at) / duration;
        return recharges;
    }

    private readonly Dictionary<int, List<(int Timestamp, double Duration)>> _rechargeHistory = [];

    private void SampleRechargeDurations(int timestamp)
    {
        foreach (var ability in _abilities.GetAbilities())
        {
            int spellId = ability.PrimarySpell.FSLID;
            var duration = ExactRechargeDuration(spellId);
            if (duration <= 0) continue;

            if (!_rechargeHistory.TryGetValue(spellId, out var history))
                _rechargeHistory[spellId] = history = [];

            if (history.Count > 0 && history[^1].Timestamp == timestamp)
                history[^1] = (timestamp, duration);
            else if (history.Count == 0 || history[^1].Duration != duration)
                history.Add((timestamp, duration));
        }
    }

    private void BeginIndependentCooldown(int key, SpellbookAbility ability, double baseMs, int ts)
    {
        if (!_timers.TryGetValue(key, out var timers))
            _timers[key] = timers = [];

        AdvanceTimers(key, ts);
        if (timers.Count >= _abilities.GetMaxCharges(key))
        {
            _drifts.Add(new CooldownDrift(key, ts, Lateness(key, timers[0].State, ts)));
            CompleteTimers(key, timers, 0, 1, ts);
        }

        var state = new CooldownState(1);
        state.Spend(ts, StartLength(ability, baseMs), EffectiveRate(key));
        var pending = NewPendingUpdate(key);
        pending.Timestamp = state.NextChargeAt ?? ts;
        Owner.EventEmitter.Schedule(pending);
        timers.Add(new ChargeTimer(state, pending));

        SyncTimers(key);
        FabricateUpdate(
            timers.Count == 1 ? UpdateSpellUsableType.BeginCooldown : UpdateSpellUsableType.UseCharge,
            key,
            ts,
            TimerSnapshot(key, timers, ts));
    }

    private void AdvanceTimers(int key, int ts)
    {
        var timers = _timers[key];
        foreach (var timer in timers.ToArray())
        {
            if (timer.State.Advance(ts) == 0)
                continue;

            timers.Remove(timer);
            Owner.EventEmitter.Cancel(timer.Pending);
            FabricateTimerRemoval(key, timers, timer, ts);
        }

        SyncTimers(key);
    }

    private void CompleteTimers(int key, List<ChargeTimer> timers, int start, int count, int eventTs)
    {
        var removed = timers.GetRange(start, count);
        timers.RemoveRange(start, count);
        foreach (var timer in removed)
            Owner.EventEmitter.Cancel(timer.Pending);

        FabricateTimerRemoval(key, timers, removed[^1], eventTs);
    }

    private void FabricateTimerRemoval(int key, List<ChargeTimer> timers, ChargeTimer removed, int eventTs)
    {
        var maxCharges = _abilities.GetMaxCharges(key);
        if (timers.Count == 0)
        {
            FabricateUpdate(
                UpdateSpellUsableType.EndCooldown,
                key,
                eventTs,
                new Snapshot(maxCharges, maxCharges, removed.State.OverallStart, removed.State.ChargeStart, eventTs, 0));
            return;
        }

        timers.Sort(CompareTimers);
        FabricateUpdate(UpdateSpellUsableType.RestoreCharge, key, eventTs, TimerSnapshot(key, timers, eventTs));
    }

    private Snapshot TimerSnapshot(int key, List<ChargeTimer> timers, int ts)
    {
        var maxCharges = _abilities.GetMaxCharges(key);
        return Describe(key, timers[0].State, ts, maxCharges - timers.Count, maxCharges);
    }

    private static int CompareTimers(ChargeTimer a, ChargeTimer b)
    {
        var byEnd = (a.State.NextChargeAt ?? int.MaxValue).CompareTo(b.State.NextChargeAt ?? int.MaxValue);
        return byEnd != 0 ? byEnd : a.State.OverallStart.CompareTo(b.State.OverallStart);
    }

    private void SyncTimers(int key)
    {
        var timers = _timers[key];
        timers.Sort(CompareTimers);
        var maxCharges = _abilities.GetMaxCharges(key);
        for (var i = 0; i < timers.Count; i++)
        {
            ApplyTimerState(key, timers, i, maxCharges);
            Owner.EventEmitter.Reschedule(timers[i].Pending);
        }
    }

    private void ApplyTimerState(int key, List<ChargeTimer> timers, int index, int maxCharges)
    {
        var timer = timers[index];
        var last = index == timers.Count - 1;
        var next = last ? timer.State : timers[index + 1].State;
        var e = timer.Pending;

        e.Timestamp = timer.State.NextChargeAt ?? int.MaxValue;
        e.OverallStartTimestamp = timer.State.OverallStart;
        e.ChargeStartTimestamp = next.ChargeStart;
        e.ExpectedRechargeTimestamp = next.NextChargeAt ?? e.Timestamp;
        e.ExpectedRechargeDuration = RechargeLengthMilliseconds(key, next);
        e.MaxCharges = maxCharges;
        e.ChargesAvailable = maxCharges - (timers.Count - index - 1);
        e.UpdateType = last ? UpdateSpellUsableType.EndCooldown : UpdateSpellUsableType.RestoreCharge;
        e.IsOnCooldown = !last;
        e.IsAvailable = true;
    }

    private sealed class ChargeTimer(CooldownState state, UpdateSpellUsableEvent pending)
    {
        public CooldownState State { get; } = state;

        public UpdateSpellUsableEvent Pending { get; } = pending;
    }
}

/// <summary>
/// The outcome of a <see cref="SpellUsable.ReduceCooldown"/> request, in milliseconds.
/// </summary>
/// <param name="Total">The cooldown progress the request asked for, in milliseconds.</param>
/// <param name="Effective">The cooldown progress that shortened a running cooldown, in milliseconds.</param>
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
