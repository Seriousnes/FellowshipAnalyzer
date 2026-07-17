using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks spell cooldown state during event dispatch, fabricating
/// <see cref="UpdateSpellUsableEvent"/> events when spells go on/off cooldown.
/// Also tracks all player casts (replacing the former TrackedStateModule).
/// </summary>
public sealed partial class SpellUsable(Lazy<Abilities> abilities, Lazy<DebugAnnotations> debugAnnotations, Lazy<Haste> haste) : Analyzer
{
    private const int CooldownLagMargin = 150;

    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];

    private double _recoveryRate = 1.0;

    public IReadOnlyList<TrackedAbilityCast> Casts => _casts;

    /// <summary>Returns the IDs of all spells currently on cooldown (any charges on cooldown).</summary>
    public IReadOnlyCollection<int> GetSpellsOnCooldown() => _cooldowns.Keys;

    /// <summary>
    /// Reduces the remaining cooldown of a spell by up to <paramref name="milliseconds"/>.
    /// For multi-charge spells, properly restores each charge in sequence as CDR overflows
    /// into the next, without requiring a workaround for skipping multiple charges at once.
    /// </summary>
    /// <returns>The actual amount of CDR applied in milliseconds.</returns>
    public int ReduceCooldown(int spellId, int milliseconds, int? timestamp = null)
    {
        if (!_cooldowns.TryGetValue(spellId, out var cd) || milliseconds <= 0)
            return 0;

        var remaining = Math.Max(0, cd.ExpectedEnd - (timestamp ?? Owner.CurrentTimestamp));

        if (milliseconds < remaining)
        {
            _cooldowns[spellId] = cd with { ExpectedEnd = cd.ExpectedEnd - milliseconds };
            return milliseconds;
        }

        EndCooldown(spellId, timestamp ?? Owner.CurrentTimestamp);
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
        var ts = atTimestamp ?? Owner.CurrentTimestamp;
        return _cooldowns.TryGetValue(spellId, out var cd)
            ? Math.Max(0, cd.ExpectedEnd - ts)
            : 0;
    }

    public void BeginCooldown(int spellId, int? timestamp = null)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var cdDuration = (int)(baseDurationMs / EffectiveRate(spellId));

            var maxCharges = _abilities.GetMaxCharges(spellId);
            cd = new CooldownInfo(
                OverallStart: ts,
                ChargeStart: ts,
                ExpectedEnd: ts + cdDuration,
                RechargeDuration: cdDuration,
                ChargesAvailable: maxCharges - 1,
                MaxCharges: maxCharges);
            _cooldowns[spellId] = cd;

            FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, ts, cd);
        }
        else if (cd.ChargesAvailable > 0)
        {
            cd = cd with { ChargesAvailable = cd.ChargesAvailable - 1 };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.UseCharge, spellId, ts, cd);
        }
        else
        {
            EndCooldown(spellId, ts);
            BeginCooldown(spellId, ts);
        }
    }

    public void EndCooldown(int spellId, int? timestamp = null, bool restoreAllCharges = false)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd)) return;

        cd = restoreAllCharges
            ? cd with { ChargesAvailable = cd.MaxCharges, ExpectedEnd = ts }
            : cd with { ChargesAvailable = cd.ChargesAvailable + 1 };

        if (cd.ChargesAvailable >= cd.MaxCharges)
        {
            cd = cd with { ExpectedEnd = ts };
            FabricateUpdate(UpdateSpellUsableType.EndCooldown, spellId, ts, cd);
            _cooldowns.Remove(spellId);
        }
        else
        {
            var nextEnd = ts + cd.RechargeDuration;
            cd = cd with { ChargeStart = ts, ExpectedEnd = nextEnd };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.RestoreCharge, spellId, ts, cd);
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
                Color: "#e74c3c",
                Summary: $"Used with no charges available: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: $"Tracker believed {e.Ability.Name} held 0/{cd.MaxCharges} charges with " +
                         $"{cd.ExpectedEnd - e.Timestamp}ms until the next recharge. Casting with no charges " +
                         $"is impossible in-game, so its configured cooldown or charge count is likely too slow.",
                Priority: 10));
        }
        else if (ability is null)
        {
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: "#e67e22",
                Summary: $"Unconfigured spell: {e.Ability.Name}  (ID: {e.Ability.Id})",
                Details: "This spell was cast by the player but is not in the hero's spellbook. " +
                         "Consider adding it to the Abilities module."));
        }
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
    /// The combined cooldown-speed multiplier for <paramref name="spellId"/>: the product of the
    /// haste-driven <b>acceleration</b> modifier and the global <b>recovery</b> modifier. A value of
    /// 9.0 means the spell's cooldown elapses 9× faster. Static reductions are folded into the
    /// ability's base cooldown; dynamic reductions (<see cref="ReduceCooldown"/>) apply afterwards
    /// to the remaining time.
    /// </summary>
    public double EffectiveRate(int spellId) => AccelerationRate(spellId) * _recoveryRate;

    /// <summary>
    /// The haste-driven cooldown-<b>acceleration</b> modifier for <paramref name="spellId"/>:
    /// <c>1 + haste</c> when the ability's cooldown is reduced by haste, otherwise 1.
    /// </summary>
    private double AccelerationRate(int spellId) =>
        _abilities.GetAbility(spellId)?.CooldownReducedByHaste == true ? 1.0 + _haste.Current : 1.0;

    /// <summary>
    /// Sets the global cooldown-<b>recovery</b> modifier (a pool separate from haste-driven
    /// acceleration). A value of 9.0 means cooldowns recover 9× faster while it is active. In-flight
    /// cooldowns are rescaled by the change as of <paramref name="timestamp"/>. The modifier is
    /// <i>set</i>, not accumulated, so a source (re)applied without a matching removal cannot
    /// compound the rate.
    /// </summary>
    public void SetCooldownRecoveryRate(double rate, int? timestamp = null)
    {
        if (rate <= 0 || rate == _recoveryRate) return;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        AdvanceCooldowns(ts);

        var change = rate / _recoveryRate;
        _recoveryRate = rate;

        foreach (var spellId in _cooldowns.Keys.ToList())
            HandleChangeRate(spellId, change, ts);
    }

    /// <summary>
    /// Rescales in-flight haste-accelerated cooldowns when the player's haste changes, so their
    /// remaining time reflects the new <c>1 + haste</c> acceleration for the rest of the cooldown.
    /// </summary>
    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        var oldAcceleration = 1.0 + (e.OldHaste ?? 0.0);
        var newAcceleration = 1.0 + (e.NewHaste ?? 0.0);
        if (oldAcceleration <= 0 || oldAcceleration == newAcceleration) return;

        var change = newAcceleration / oldAcceleration;
        AdvanceCooldowns(e.Timestamp);

        foreach (var spellId in _cooldowns.Keys.ToList())
        {
            if (_abilities.GetAbility(spellId)?.CooldownReducedByHaste == true)
                HandleChangeRate(spellId, change, e.Timestamp);
        }
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
        int MaxCharges);
}

/// <summary>
/// A point-in-time record of a single player cast.
/// </summary>
public readonly record struct TrackedAbilityCast(
    int Timestamp,
    int Id,
    int TargetId);
