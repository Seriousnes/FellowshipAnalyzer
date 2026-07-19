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
    Lazy<CooldownReduction> cooldownReduction) : Analyzer
{
    private const int CooldownLagMargin = 150;

    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];

    private double _addedRecovery;

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
        var generated = _cooldownReduction.Scale(_abilities.GetAbility(spellId), milliseconds);
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
        return baseDurationMs <= 0 ? 0 : (int)(_cooldownReduction.Scale(ability, baseDurationMs) / EffectiveRate(spellId));
    }

    public void BeginCooldown(int spellId, int? timestamp = null)
    {
        var ts = timestamp ?? Owner.CurrentTimestamp;
        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var ability = _abilities.GetAbility(spellId);
            var baseDurationMs = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (baseDurationMs <= 0) return;
            var cdDuration = (int)(_cooldownReduction.Scale(ability, baseDurationMs) / EffectiveRate(spellId));

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
    /// <remarks>
    /// Restoring a charge on a multi-charge spell starts the next charge's recharge, which may itself
    /// already be due by <paramref name="timestamp"/>, so this sweeps repeatedly until nothing is left
    /// due. A single pass would restore at most one charge per event and strand the rest, making a spell
    /// look unavailable long after the game had given the charges back. Each pass restores at least one
    /// charge and charges are capped, so the loop always drains.
    /// </remarks>
    private void AdvanceCooldowns(int timestamp)
    {
        while (true)
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
            {
                if (_cooldowns.TryGetValue(spellId, out var cd))
                    EndCooldown(spellId, cd.ExpectedEnd);
            }
        }
    }

    /// <summary>
    /// The cooldown-speed multiplier for <paramref name="spellId"/>: <c>1 + CDA</c>, where the Cooldown
    /// Acceleration pool sums the haste term (the player's haste when the ability is flagged
    /// <c>CooldownReducedByHaste</c>, else 0), the selected combatant's gear acceleration that applies to this
    /// ability (<see cref="CombatantStats.CooldownAcceleration"/> totalled for the spell; today a legendary's
    /// unscoped Strand of Eternity, but scoped entries contribute only to the abilities they match), and added
    /// recovery (Chronoshift). Recovery and acceleration are one mechanic fed by a single additive pool, so each
    /// source contributes a term rather than an independent factor; a value of 9.0 means the spell's
    /// cooldown elapses 9× faster. Unlike Ability Cooldown Reduction, which <see cref="ReduceCooldown"/> and
    /// <see cref="BeginCooldown"/> snapshot at cast, CDA is dynamic: a change to any term rescales the
    /// affected in-flight cooldowns.
    /// </summary>
    public double EffectiveRate(int spellId) =>
        1.0 + HasteRecovery(spellId)
            + Owner.SelectedCombatant.Stats.CooldownAcceleration.Total(_abilities.GetAbility(spellId))
            + _addedRecovery;

    /// <summary>
    /// Haste's contribution to <paramref name="spellId"/>'s recovery pool: the player's current haste
    /// when the ability's cooldown is reduced by haste, otherwise 0.
    /// </summary>
    private double HasteRecovery(int spellId) =>
        _abilities.GetAbility(spellId)?.CooldownReducedByHaste == true ? _haste.Current : 0.0;

    /// <summary>
    /// Sets the cooldown recovery contributed by sources other than haste, as an added term on the
    /// shared pool (Chronoshift adds 8.0 while channeling, taking a non-hasted ability to 9× recovery).
    /// In-flight cooldowns are rescaled by their own change in <see cref="EffectiveRate"/> as of
    /// <paramref name="timestamp"/>, which differs per spell because haste is in the same pool. The
    /// value is <i>set</i>, not accumulated, so a source (re)applied without a matching removal cannot
    /// compound it.
    /// </summary>
    public void SetAddedCooldownRecovery(double added, int? timestamp = null)
    {
        if (added < 0 || added == _addedRecovery) return;
        var ts = timestamp ?? Owner.CurrentTimestamp;
        AdvanceCooldowns(ts);

        var previousRates = _cooldowns.Keys.ToDictionary(id => id, EffectiveRate);
        _addedRecovery = added;

        foreach (var (spellId, previousRate) in previousRates)
        {
            if (_cooldowns.ContainsKey(spellId))
                HandleChangeRate(spellId, EffectiveRate(spellId) / previousRate, ts);
        }
    }

    /// <summary>
    /// Rescales in-flight haste-reduced cooldowns when the player's haste changes, so their remaining
    /// time reflects the new recovery rate for the rest of the cooldown.
    /// </summary>
    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        AdvanceCooldowns(e.Timestamp);

        foreach (var spellId in _cooldowns.Keys.ToList())
        {
            var ability = _abilities.GetAbility(spellId);
            if (ability?.CooldownReducedByHaste != true) continue;

            var acceleration = Owner.SelectedCombatant.Stats.CooldownAcceleration.Total(ability);
            var oldRate = 1.0 + (e.OldHaste ?? 0.0) + acceleration + _addedRecovery;
            var newRate = 1.0 + (e.NewHaste ?? 0.0) + acceleration + _addedRecovery;
            if (oldRate <= 0 || oldRate == newRate) continue;

            HandleChangeRate(spellId, newRate / oldRate, e.Timestamp);
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
