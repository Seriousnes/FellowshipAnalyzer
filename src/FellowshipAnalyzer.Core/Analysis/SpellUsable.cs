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
public partial class SpellUsable(
    Lazy<Abilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private const int CooldownLagMargin = 150;

    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];
    private Dictionary<int, int>? _deferredStarts;

    /// <summary>Every player cast during dispatch, in the order it occurred.</summary>
    public List<TrackedAbilityCast> Casts => _casts;

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public virtual List<int> GetSpellsOnCooldown() => [.. _cooldowns.Keys];

    /// <summary>
    /// Reduces the remaining cooldown of a spell by <paramref name="milliseconds"/> of cooldown progress
    /// (e.g. Rolling Flames). Ability Cooldown Reduction does not scale the request. The cooldown recovers
    /// at <see cref="EffectiveRate"/>, so the request shortens the time left by
    /// <c>milliseconds / EffectiveRate</c>: 1000ms is worth 500ms while the cooldown recovers at 2×. For
    /// multi-charge spells each charge is restored in sequence as the reduction overflows into the next. An
    /// ability declaring <see cref="SpellbookAbility.IndependentCharges"/> takes the reduction on its earliest
    /// timer, and a reduction past that timer's end completes it and shortens the next.
    /// </summary>
    /// <returns>
    /// The requested cooldown progress, and how much of it shortened a running cooldown, both in
    /// milliseconds of cooldown progress. The two differ when the spell was already available, or had less
    /// progress left than the request.
    /// </returns>
    public virtual CooldownReductionResult ReduceCooldown(int spellId, int milliseconds, int? timestamp = null)
    {
        if (milliseconds <= 0)
            return new(0, 0);

        var rate = EffectiveRate(spellId);
        var shortenedMs = (int)Math.Round(milliseconds / rate);
        var appliedMs = ApplyReduction(spellId, shortenedMs, timestamp);
        var effective = appliedMs >= shortenedMs ? milliseconds : Math.Min(milliseconds, (int)Math.Round(appliedMs * rate));
        return new(milliseconds, effective);
    }

    private int ApplyReduction(int spellId, int milliseconds, int? timestamp)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd) || cd.Held || milliseconds <= 0)
            return 0;

        if (cd.HasIndependentTimers)
            return ApplyIndependentReduction(spellId, cd, milliseconds, timestamp ?? Owner.CurrentTimestamp);

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

    private int ApplyIndependentReduction(int spellId, CooldownInfo cd, int milliseconds, int timestamp)
    {
        var earliest = cd.Timers[0];
        var remaining = Math.Max(0, earliest.ExpectedEnd - timestamp);

        if (milliseconds < remaining)
        {
            var shortened = earliest with { ExpectedEnd = earliest.ExpectedEnd - milliseconds };
            _cooldowns[spellId] = WithTimers(cd, [shortened, .. cd.Timers[1..]]);
            RefreshTimers(spellId);
            return milliseconds;
        }

        EndCooldown(spellId, timestamp);
        return remaining + ApplyReduction(spellId, milliseconds - remaining, timestamp);
    }

    /// <summary>Whether <paramref name="spellId"/> has at least one charge available to cast right now.</summary>
    public virtual bool IsAvailable(int spellId) => !_cooldowns.TryGetValue(spellId, out var cd) || cd.ChargesAvailable > 0;

    /// <summary>Whether any charge of <paramref name="spellId"/> is currently recharging.</summary>
    public virtual bool IsOnCooldown(int spellId) => _cooldowns.ContainsKey(spellId);

    /// <summary>How many charges of <paramref name="spellId"/> can be cast right now.</summary>
    public virtual int ChargesAvailable(int spellId) =>
        _cooldowns.TryGetValue(spellId, out var cd)
            ? cd.ChargesAvailable
            : _abilities.GetMaxCharges(spellId);

    /// <summary>
    /// Milliseconds until <paramref name="spellId"/>'s next charge recharges, evaluated at
    /// <paramref name="atTimestamp"/> (defaulting to the current dispatch time). Zero when a charge is
    /// already available.
    /// </summary>
    public virtual int CooldownRemaining(int spellId, int? atTimestamp = null)
    {
        var ts = atTimestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd))
            return 0;

        return cd.Held ? RechargeDuration(spellId) : Math.Max(0, cd.ExpectedEnd - ts);
    }

    /// <summary>
    /// The full recharge duration one charge of <paramref name="spellId"/> currently takes: its base
    /// cooldown after Ability Cooldown Reduction, divided by the current <see cref="EffectiveRate"/>.
    /// This is the value <see cref="BeginCooldown"/> assigns a fresh recharge, exposed for metrics that
    /// need the effective (haste/gear/recovery-accelerated) period rather than the raw curated cooldown.
    /// Returns 0 for a spell with no configured cooldown.
    /// </summary>
    public virtual int RechargeDuration(int spellId)
    {
        var ability = _abilities.GetAbility(spellId);
        var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
        return baseDurationMs <= 0 ? 0 : StartDuration(ability, baseDurationMs, EffectiveRate(spellId));
    }

    /// <summary>
    /// Consumes a charge of <paramref name="spellId"/>: starts a fresh recharge if none is running, spends an
    /// already-available charge if one exists, or, if every charge is spent, forces the current recharge to
    /// complete before beginning a new one, so the tracker stays in sync with an in-game cast it did not
    /// expect to be possible. An ability declaring <see cref="SpellbookAbility.CooldownStartsWhenBuffEnds"/>
    /// consumes its charge and holds, with nothing recharging until that buff leaves the player. An ability
    /// declaring <see cref="SpellbookAbility.IndependentCharges"/> starts a timer of its own for the charge
    /// this cast spent, alongside any timer already running.
    /// </summary>
    public virtual void BeginCooldown(int spellId, int? timestamp = null)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (_abilities.GetAbility(spellId)?.IndependentCharges == true)
        {
            BeginIndependentCooldown(spellId, ts);
            return;
        }

        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var ability = _abilities.GetAbility(spellId);
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var rate = EffectiveRate(spellId);
            var cdDuration = StartDuration(ability, baseDurationMs, rate);

            var maxCharges = _abilities.GetMaxCharges(spellId);

            if (ability?.CooldownStartsWhenBuffEnds is not null)
            {
                cd = new CooldownInfo(
                    OverallStart: ts,
                    ChargeStart: ts,
                    ExpectedEnd: ts,
                    RechargeDuration: cdDuration,
                    ChargesAvailable: maxCharges - 1,
                    MaxCharges: maxCharges,
                    Rate: rate,
                    PendingEnd: null,
                    Held: true);
                _cooldowns[spellId] = cd;
                FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, ts, cd);
                return;
            }

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
    /// end is scheduled for the next charge. An ability declaring
    /// <see cref="SpellbookAbility.IndependentCharges"/> completes its earliest timer, or every timer with
    /// <paramref name="restoreAllCharges"/>, and the timers left keep running.
    /// </summary>
    public virtual void EndCooldown(int spellId, int? timestamp = null, bool restoreAllCharges = false)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return;

        if (cd.HasIndependentTimers)
        {
            CompleteTimers(spellId, cd, ts, 0, restoreAllCharges ? cd.Timers.Length : 1);
            return;
        }

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
            var nextDuration = RechargeDuration(spellId);
            cd = cd with { ChargeStart = ts, ExpectedEnd = ts + nextDuration, RechargeDuration = nextDuration, Rate = EffectiveRate(spellId), Held = false };
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
    /// recharging, so the pending expiry is cancelled and the spell drops to fully available. An ability
    /// declaring <see cref="SpellbookAbility.IndependentCharges"/> drops its latest timer, and the timers
    /// left keep the progress they had made.
    /// </summary>
    /// <returns><c>true</c> when a charge was handed back, <c>false</c> when every charge was already available.</returns>
    public virtual bool RefundCharge(int spellId, int? timestamp = null)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return false;

        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (cd.HasIndependentTimers)
        {
            CompleteTimers(spellId, cd, ts, cd.Timers.Length - 1, 1);
            return true;
        }

        var eventTs = Owner.CurrentTimestamp;
        cd = cd with { ChargesAvailable = cd.ChargesAvailable + 1, Held = false };

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

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnDeferredCooldownBuffRemoved(RemoveBuffEvent e)
    {
        if (!DeferredStarts.TryGetValue(e.Ability.Id, out var spellId))
            return;

        if (!_cooldowns.TryGetValue(spellId, out var cd) || !cd.Held)
            return;

        var ability = _abilities.GetAbility(spellId);
        var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
        var rate = EffectiveRate(spellId);
        var cdDuration = StartDuration(ability, baseDurationMs, rate);

        cd = cd with
        {
            OverallStart = e.Timestamp,
            ChargeStart = e.Timestamp,
            ExpectedEnd = e.Timestamp + cdDuration,
            RechargeDuration = cdDuration,
            Rate = rate,
            Held = false,
        };
        _cooldowns[spellId] = cd;

        FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, e.Timestamp, cd);
        RefreshPendingEnd(spellId);
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
        var spellId = e.Ability.Id;
        if (!_cooldowns.TryGetValue(spellId, out var cd))
            return;

        if (cd.HasIndependentTimers)
        {
            if (cd.Timers.Any(t => ReferenceEquals(t.Pending, e)))
                ReleaseTimers(spellId, cd, [.. cd.Timers.Where(t => !ReferenceEquals(t.Pending, e))]);
            return;
        }

        if (!ReferenceEquals(cd.PendingEnd, e))
            return;

        if (e.UpdateType == UpdateSpellUsableType.EndCooldown)
        {
            _cooldowns.Remove(spellId);
            return;
        }

        var restoreTs = e.Timestamp;
        var nextDuration = RechargeDuration(spellId);
        _cooldowns[spellId] = cd with
        {
            ChargesAvailable = cd.ChargesAvailable + 1,
            ChargeStart = restoreTs,
            ExpectedEnd = restoreTs + nextDuration,
            RechargeDuration = nextDuration,
            Rate = EffectiveRate(spellId),
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
            ApplyPendingEndState(spellId, cd.PendingEnd, cd);
            Owner.EventEmitter.Reschedule(cd.PendingEnd);
        }
    }

    private UpdateSpellUsableEvent CreatePendingEnd(int spellId, CooldownInfo cd)
    {
        var e = NewPendingUpdate(spellId);
        ApplyPendingEndState(spellId, e, cd);
        return e;
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

    private void ApplyPendingEndState(int spellId, UpdateSpellUsableEvent e, CooldownInfo cd)
    {
        var endTs = cd.ExpectedEnd;
        var chargesAfter = cd.ChargesAvailable + 1;

        e.Timestamp = endTs;
        e.OverallStartTimestamp = cd.OverallStart;
        e.MaxCharges = cd.MaxCharges;
        e.ChargesAvailable = chargesAfter;

        if (chargesAfter >= cd.MaxCharges)
        {
            e.UpdateType = UpdateSpellUsableType.EndCooldown;
            e.IsOnCooldown = false;
            e.IsAvailable = true;
            e.ChargeStartTimestamp = cd.ChargeStart;
            e.ExpectedRechargeTimestamp = endTs;
            e.ExpectedRechargeDuration = cd.RechargeDuration;
        }
        else
        {
            var nextDuration = RechargeDuration(spellId);
            e.UpdateType = UpdateSpellUsableType.RestoreCharge;
            e.IsOnCooldown = true;
            e.IsAvailable = true;
            e.ChargeStartTimestamp = endTs;
            e.ExpectedRechargeTimestamp = endTs + nextDuration;
            e.ExpectedRechargeDuration = nextDuration;
        }
    }

    /// <summary>
    /// The cooldown-speed multiplier for <paramref name="spellId"/>: <c>1 + CDA</c>, where the Cooldown
    /// Acceleration pool sums the haste term (the player's haste when Haste enters the ability's time
    /// rate, else 0) and the pool <see cref="StatTracker"/> tracks for this ability:
    /// the gear-derived seed (today a legendary's unscoped Strand of Eternity) plus tracked runtime modifiers
    /// such as Chronoshift, with scoped entries contributing only to the abilities they match. A Weapon
    /// ability takes only the terms its own effects grant. Recovery and acceleration are one mechanic fed by
    /// a single additive pool, so each source contributes a term rather than an independent factor: a
    /// Cooldown Recovery multiplier of <c>m</c> adds <c>m - 1</c>, and a value of 8.0 means the spell's
    /// cooldown elapses 8× faster. Unlike
    /// Ability Cooldown Reduction, which <see cref="ReduceCooldown"/> and <see cref="BeginCooldown"/>
    /// fix at cast, CDA is dynamic: a change to any term rescales the affected in-flight cooldowns.
    /// </summary>
    public virtual double EffectiveRate(int spellId) =>
        1.0 + HasteRecovery(spellId)
            + _statTracker.CurrentCooldownAcceleration(_abilities.GetAbility(spellId));

    private double HasteRecovery(int spellId) =>
        _abilities.GetAbility(spellId)?.CooldownHaste == CooldownHasteScaling.TimeRate ? _haste.Current : 0.0;

    /// <summary>
    /// The milliseconds a cooldown starting now runs at <paramref name="rate"/>: the base after Ability
    /// Cooldown Reduction, divided by <c>1 + haste</c> when Haste scales the duration, then by the rate.
    /// </summary>
    private int StartDuration(SpellbookAbility? ability, int baseDurationMs, double rate) =>
        (int)(_statTracker.ScaleByCooldownReduction(ability, baseDurationMs)
            / (ability?.CooldownHaste == CooldownHasteScaling.Duration ? 1.0 + _haste.Current : 1.0)
            / rate);

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        RescaleChangedCooldowns(e.Timestamp);
        SampleRechargeDurations(e.Timestamp);
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        RescaleChangedCooldowns(e.Timestamp);
        SampleRechargeDurations(e.Timestamp);
    }

    [On<DungeonStartEvent>]
    private void OnDungeonStartSample(DungeonStartEvent e) => SampleRechargeDurations(e.Timestamp);

    /// <summary>
    /// The time-weighted mean of <see cref="RechargeDuration"/> for <paramref name="spellId"/> across
    /// <paramref name="start"/> to <paramref name="end"/>, from the recharge durations in force while
    /// the dungeon ran. Reads the current <see cref="RechargeDuration"/> when the window holds none.
    /// </summary>
    public int RechargeDurationDuring(int spellId, int start, int end)
    {
        var key = _abilities.GetAbility(spellId)?.PrimarySpell.FSLID.Value ?? spellId;
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

            weighted += (double)duration * (timestamp - at);
            at = timestamp;
            duration = value;
        }

        weighted += (double)duration * (end - at);
        return (int)Math.Round(weighted / (end - start));
    }

    /// <summary>
    /// The time-weighted mean of <see cref="RechargeDuration"/> for <paramref name="spellId"/> across
    /// <paramref name="pull"/>, so a pull analyzer reads the recharge its own pull's stats gave.
    /// </summary>
    public int RechargeDurationDuring(int spellId, PullStartEvent pull) =>
        RechargeDurationDuring(spellId, pull.StartTime, pull.EndTime);

    private readonly Dictionary<int, List<(int Timestamp, int Duration)>> _rechargeHistory = [];

    private void SampleRechargeDurations(int timestamp)
    {
        foreach (var ability in _abilities.GetAbilities())
        {
            int spellId = ability.PrimarySpell.FSLID;
            var duration = RechargeDuration(spellId);
            if (duration <= 0) continue;

            if (!_rechargeHistory.TryGetValue(spellId, out var history))
                _rechargeHistory[spellId] = history = [];

            if (history.Count > 0 && history[^1].Timestamp == timestamp)
                history[^1] = (timestamp, duration);
            else if (history.Count == 0 || history[^1].Duration != duration)
                history.Add((timestamp, duration));
        }
    }

    private void RescaleChangedCooldowns(int timestamp)
    {
        foreach (var spellId in _cooldowns.Keys.ToList())
        {
            if (!_cooldowns.TryGetValue(spellId, out var cd)) continue;

            if (cd.Held) continue;

            var newRate = EffectiveRate(spellId);
            if (newRate > 0 && newRate != cd.Rate)
                HandleChangeRate(spellId, newRate, timestamp);
            else if (!cd.HasIndependentTimers)
                RefreshPendingEnd(spellId);
        }
    }

    private void HandleChangeRate(int spellId, double newRate, int timestamp)
    {
        var cd = _cooldowns[spellId];
        if (cd.HasIndependentTimers)
        {
            HandleIndependentChangeRate(spellId, cd, newRate, timestamp);
            return;
        }

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

    private void BeginIndependentCooldown(int spellId, int ts)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var ability = _abilities.GetAbility(spellId);
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var rate = EffectiveRate(spellId);
            var cdDuration = StartDuration(ability, baseDurationMs, rate);
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
            _cooldowns[spellId] = WithTimers(cd, [StartTimer(spellId, ts, cdDuration)]);

            FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, ts, _cooldowns[spellId]);
            RefreshTimers(spellId);
        }
        else if (cd.ChargesAvailable > 0)
        {
            ChargeTimer[] timers = [.. cd.Timers, StartTimer(spellId, ts, RechargeDuration(spellId))];
            _cooldowns[spellId] = WithTimers(cd with { ChargesAvailable = cd.ChargesAvailable - 1 }, timers);
            FabricateUpdate(UpdateSpellUsableType.UseCharge, spellId, ts, _cooldowns[spellId]);
            RefreshTimers(spellId);
        }
        else
        {
            EndCooldown(spellId, ts);
            BeginIndependentCooldown(spellId, ts);
        }
    }

    private ChargeTimer StartTimer(int spellId, int start, int duration)
    {
        var pending = NewPendingUpdate(spellId);
        pending.Timestamp = start + duration;
        Owner.EventEmitter.Schedule(pending);
        return new ChargeTimer(start, start + duration, duration, pending);
    }

    private void CompleteTimers(int spellId, CooldownInfo cd, int ts, int start, int count)
    {
        var eventTs = Owner.CurrentTimestamp;
        for (var i = start; i < start + count; i++)
            Owner.EventEmitter.Cancel(cd.Timers[i].Pending);

        ChargeTimer[] remaining = [.. cd.Timers[..start], .. cd.Timers[(start + count)..]];
        if (remaining.Length == 0)
        {
            cd = cd with { ChargesAvailable = cd.MaxCharges, ExpectedEnd = ts, Timers = remaining };
            FabricateUpdate(UpdateSpellUsableType.EndCooldown, spellId, eventTs, cd);
            _cooldowns.Remove(spellId);
            return;
        }

        ReleaseTimers(spellId, cd, remaining);
        FabricateUpdate(UpdateSpellUsableType.RestoreCharge, spellId, eventTs, _cooldowns[spellId]);
    }

    private void ReleaseTimers(int spellId, CooldownInfo cd, ChargeTimer[] remaining)
    {
        if (remaining.Length == 0)
        {
            _cooldowns.Remove(spellId);
            return;
        }

        _cooldowns[spellId] = WithTimers(cd with { ChargesAvailable = cd.MaxCharges - remaining.Length }, remaining);
        RefreshTimers(spellId);
    }

    private void HandleIndependentChangeRate(int spellId, CooldownInfo cd, double newRate, int timestamp)
    {
        var rateChange = newRate / cd.Rate;
        var timers = Array.ConvertAll(cd.Timers, t => RescaleTimer(t, rateChange, timestamp));
        _cooldowns[spellId] = WithTimers(cd with { Rate = newRate }, timers);
        FabricateUpdate(UpdateSpellUsableType.ChangeCooldownRate, spellId, timestamp, _cooldowns[spellId]);
        RefreshTimers(spellId);
    }

    private static ChargeTimer RescaleTimer(ChargeTimer timer, double rateChange, int timestamp)
    {
        var remaining = Math.Max(0, timer.ExpectedEnd - timestamp);
        var percentRemaining = timer.RechargeDuration == 0 ? 0 : (double)remaining / timer.RechargeDuration;
        var newRecharge = (int)Math.Round(timer.RechargeDuration / rateChange);
        var newRemaining = (int)Math.Round(newRecharge * percentRemaining);
        return timer with { RechargeDuration = newRecharge, ExpectedEnd = timestamp + newRemaining };
    }

    private void RefreshTimers(int spellId)
    {
        var cd = _cooldowns[spellId];
        for (var i = 0; i < cd.Timers.Length; i++)
        {
            ApplyTimerState(cd, i);
            Owner.EventEmitter.Reschedule(cd.Timers[i].Pending);
        }
    }

    private static void ApplyTimerState(CooldownInfo cd, int index)
    {
        var timer = cd.Timers[index];
        var last = index == cd.Timers.Length - 1;
        var next = last ? timer : cd.Timers[index + 1];
        var e = timer.Pending;

        e.Timestamp = timer.ExpectedEnd;
        e.OverallStartTimestamp = timer.Start;
        e.ChargeStartTimestamp = next.Start;
        e.ExpectedRechargeTimestamp = next.ExpectedEnd;
        e.ExpectedRechargeDuration = next.RechargeDuration;
        e.MaxCharges = cd.MaxCharges;
        e.ChargesAvailable = cd.MaxCharges - (cd.Timers.Length - index - 1);
        e.UpdateType = last ? UpdateSpellUsableType.EndCooldown : UpdateSpellUsableType.RestoreCharge;
        e.IsOnCooldown = !last;
        e.IsAvailable = true;
    }

    private static CooldownInfo WithTimers(CooldownInfo cd, ChargeTimer[] timers)
    {
        ChargeTimer[] sorted = [.. timers.OrderBy(static t => t.ExpectedEnd).ThenBy(static t => t.Start)];
        var earliest = sorted[0];
        return cd with
        {
            OverallStart = earliest.Start,
            ChargeStart = earliest.Start,
            ExpectedEnd = earliest.ExpectedEnd,
            RechargeDuration = earliest.RechargeDuration,
            Timers = sorted,
        };
    }

    private record struct CooldownInfo(
        int OverallStart,
        int ChargeStart,
        int ExpectedEnd,
        int RechargeDuration,
        int ChargesAvailable,
        int MaxCharges,
        double Rate,
        UpdateSpellUsableEvent? PendingEnd,
        bool Held = false)
    {
        public ChargeTimer[] Timers { get; init; } = [];

        public bool HasIndependentTimers => Timers.Length > 0;
    }

    private sealed record ChargeTimer(int Start, int ExpectedEnd, int RechargeDuration, UpdateSpellUsableEvent Pending);
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
