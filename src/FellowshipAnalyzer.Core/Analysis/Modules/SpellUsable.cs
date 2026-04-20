using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks spell cooldown state during event dispatch, fabricating
/// <see cref="UpdateSpellUsableEvent"/> events when spells go on/off cooldown.
/// Also tracks all player casts (replacing the former TrackedStateModule).
/// </summary>
public sealed class SpellUsable : Analyzer
{
    private readonly Dictionary<int, CooldownInfo> _cooldowns = [];
    private readonly List<TrackedAbilityCast> _casts = [];
    private readonly Dictionary<(int, int), int> _pendingBeginCastTimestamps = [];

    private Abilities _abilities = null!;
    private DebugAnnotations _debugAnnotations = null!;

    public override void Initialize()
    {
        _abilities = Owner.GetModule<Abilities>()!;
        _debugAnnotations = Owner.GetModule<DebugAnnotations>()!;

        AddEventListener(Events.BeginCast.By(SELECTED_PLAYER), OnBeginCast);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
        AddEventListener(Events.PrefilterCD.By(SELECTED_PLAYER), OnFilterCooldown);
        AddEventListener(Events.Any, OnAnyEvent);
    }

    public IReadOnlyList<TrackedAbilityCast> Casts => _casts;

    public bool IsAvailable(int spellId) => !_cooldowns.TryGetValue(spellId, out var cd) || cd.ChargesAvailable > 0;

    public bool IsOnCooldown(int spellId) => _cooldowns.ContainsKey(spellId);

    public int ChargesAvailable(int spellId) =>
        _cooldowns.TryGetValue(spellId, out var cd)
            ? cd.ChargesAvailable
            : _abilities.GetMaxCharges(spellId);

    public int CooldownRemaining(int spellId, int? atTimestamp = null)
    {
        var ts = atTimestamp ?? _currentTimestamp;
        return _cooldowns.TryGetValue(spellId, out var cd)
            ? Math.Max(0, cd.ExpectedEnd - ts)
            : 0;
    }

    public void BeginCooldown(int spellId, int timestamp, int castStart = 0)
    {
        if (castStart <= 0) castStart = timestamp;

        if (!_cooldowns.TryGetValue(spellId, out var cd))
        {
            var cdDuration = (int)(_abilities.GetExpectedCooldown(spellId) * 1000);
            if (cdDuration <= 0) return;

            var maxCharges = _abilities.GetMaxCharges(spellId);
            cd = new CooldownInfo(
                OverallStart: timestamp,
                ChargeStart: timestamp,
                ExpectedEnd: timestamp + cdDuration,
                RechargeDuration: cdDuration,
                ChargesAvailable: maxCharges - 1,
                MaxCharges: maxCharges);
            _cooldowns[spellId] = cd;

            FabricateUpdate(UpdateSpellUsableType.BeginCooldown, spellId, timestamp, cd, castStart);
        }
        else if (cd.ChargesAvailable > 0)
        {
            cd = cd with { ChargesAvailable = cd.ChargesAvailable - 1 };
            _cooldowns[spellId] = cd;
            FabricateUpdate(UpdateSpellUsableType.UseCharge, spellId, timestamp, cd, castStart);
        }
        else
        {
            EndCooldown(spellId, timestamp);
            BeginCooldown(spellId, timestamp, castStart);
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

    private void OnBeginCast(BeginCastEvent e)
    {
        if (e.Ability is not null)
            _pendingBeginCastTimestamps[(e.Ability.Id, e.SourceId)] = e.Timestamp;
    }

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

        _pendingBeginCastTimestamps.Remove((e.Ability.Id, e.SourceId), out var castStart);
        BeginCooldown(e.Ability.Id, e.Timestamp, castStart);
    }

    private void OnFilterCooldown(FilterCooldownInfoEvent e) =>
        BeginCooldown(e.Ability.Id, e.Timestamp);

    private int _currentTimestamp;

    private void OnAnyEvent(Event e)
    {
        _currentTimestamp = e.Timestamp;
        AdvanceCooldowns(e.Timestamp);
    }

    /// <summary>
    /// Checks whether any in-flight cooldowns have naturally expired and fires
    /// <see cref="UpdateSpellUsableEvent"/> for each one.
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

        foreach (var spellId in expired)
            EndCooldown(spellId, _cooldowns[spellId].ExpectedEnd);
    }

    private void FabricateUpdate(UpdateSpellUsableType updateType, int spellId, int timestamp, CooldownInfo cd, int castStart = 0)
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
            CastStartTimestamp = castStart > 0 ? castStart : timestamp,
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
