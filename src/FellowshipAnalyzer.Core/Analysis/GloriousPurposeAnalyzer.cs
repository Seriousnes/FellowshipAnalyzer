using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models the cooldown recovery Glorious Purpose grants. The Fated Strike weapon's active applies the
/// buff for a fixed 6 seconds, taking cooldown recovery to 3× for an ability with no haste contribution,
/// so it joins the pool <see cref="StatTracker"/> tracks as an added term of 2.0.
/// <para>
/// The window is driven by buff events. <see cref="OnApply"/> and <see cref="OnRefresh"/> open it and add
/// the modifier, and both fabricate a <see cref="RemoveBuffEvent"/> at the scheduled 6-second expiry so
/// the close rides a real, in-order event rather than a lazy per-event poll. A refresh moves the expiry
/// forward and leaves the earlier fabricated removal in the stream, so <see cref="OnRemove"/> ignores any
/// fabricated removal that is no longer the scheduled one. An unmatched or repeated apply cannot compound
/// the modifier because <see cref="CloseWindow"/> only removes while a window is open and a removal of an
/// absent modifier is a no-op.
/// </para>
/// </summary>
[After<SpellUsable>]
public sealed partial class GloriousPurposeAnalyzer(
    Lazy<SpellUsable> spellUsable,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private const double AddedRecovery = 2.0;

    private static readonly CooldownModifier AddedRecoveryModifier = new(AddedRecovery);

    private const int BuffDurationMs = 6000;

    private readonly List<GloriousPurposeWindow> _windows = [];
    private readonly Dictionary<int, int> _recoveryBySpell = [];

    private readonly Dictionary<int, int> _snapshot = [];
    private RemoveBuffEvent? _scheduledRemoval;
    private int? _scheduledEnd;
    private int _openTimestamp;

    /// <summary>All Glorious Purpose recovery windows observed for the selected player.</summary>
    public IReadOnlyList<GloriousPurposeWindow> Windows => _windows;

    /// <summary>The number of times the Fated Strike weapon's active was cast over the encounter.</summary>
    public int Activations { get; private set; }

    /// <summary>
    /// Bonus cooldown recovery Glorious Purpose granted to each ability over the encounter, in
    /// milliseconds, ordered by amount descending. This is the extra cooldown progress the added 200%
    /// bought beyond what the spell would have made over the same window without it. Because the pool is
    /// additive, that extra is 2× the time on cooldown whatever the ability's haste contribution.
    /// </summary>
    public IReadOnlyList<AbilityRecovery> RecoveryByAbility =>
        [.. _recoveryBySpell
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AbilityRecovery(kv.Key, kv.Value))];

    /// <summary>Total bonus cooldown recovery Glorious Purpose granted across all abilities, in milliseconds.</summary>
    public int TotalRecoveredMs => _recoveryBySpell.Values.Sum();

    /// <summary>Total time the buff was active across every window, in milliseconds.</summary>
    public int TotalUptimeMs => _windows.Sum(window => window.EndTimestamp - window.BeginTimestamp);

    [On<CastEvent>(By = Actor.Player)]
    private void OnFatedStrikeCast(CastEvent castEvent)
    {
        if (castEvent.Ability.FSLID == Items.FatedStrike.FSLID)
            Activations++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApply(ApplyBuffEvent buffEvent)
    {
        if (IsGloriousPurpose(buffEvent))
            OpenOrExtendWindow(buffEvent);
    }

    [On<RefreshBuffEvent>(To = Actor.Player)]
    private void OnRefresh(RefreshBuffEvent buffEvent)
    {
        if (IsGloriousPurpose(buffEvent))
            OpenOrExtendWindow(buffEvent);
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemove(RemoveBuffEvent buffEvent)
    {
        if (!IsGloriousPurpose(buffEvent)) return;
        if (buffEvent.Fabricated == true && !ReferenceEquals(buffEvent, _scheduledRemoval)) return;

        CloseWindow(buffEvent.Timestamp, buffEvent);
    }

    /// <remarks>
    /// The id is matched here rather than through the <c>[On]</c> attribute's <c>Spell</c> filter because
    /// <see cref="Items"/> is emitted by the spell-registry generator, and source generators do not see
    /// each other's output within one compilation pass. Hero projects reference Core as compiled metadata
    /// and can use the filter; Core itself cannot.
    /// </remarks>
    private static bool IsGloriousPurpose(BuffEvent buffEvent) =>
        buffEvent.Ability.FSLID == Items.GloriousPurpose.FSLID;

    private void OpenOrExtendWindow(BuffEvent buffEvent)
    {
        if (_scheduledEnd is null)
            OpenWindow(buffEvent);
        else
            ExtendWindow(buffEvent);
    }

    /// <summary>
    /// Pushes the expiry of the live window out to a full duration from <paramref name="buffEvent"/>,
    /// leaving the open modifier and the cooldown snapshot in place. Re-fabricating the expiry supersedes
    /// the earlier scheduled removal, which <see cref="OnRemove"/> then ignores as stale.
    /// </summary>
    private void ExtendWindow(BuffEvent buffEvent)
    {
        _scheduledEnd = buffEvent.Timestamp + BuffDurationMs;
        _windows[^1] = _windows[^1] with { EndTimestamp = _scheduledEnd.Value };

        FabricateExpiry(buffEvent);
    }

    private void OpenWindow(BuffEvent buffEvent)
    {
        _openTimestamp = buffEvent.Timestamp;
        _scheduledEnd = buffEvent.Timestamp + BuffDurationMs;
        _windows.Add(new GloriousPurposeWindow(buffEvent.Timestamp, _scheduledEnd.Value));

        _statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, AddedRecoveryModifier, buffEvent);

        FabricateExpiry(buffEvent);
    }

    private void FabricateExpiry(BuffEvent trigger) =>
        _scheduledRemoval = Owner.EventEmitter.FabricateEvent(new RemoveBuffEvent
        {
            Timestamp = _scheduledEnd!.Value,
            SourceId = trigger.SourceId,
            TargetId = trigger.TargetId,
            Ability = trigger.Ability,
            AbilityGameId = trigger.AbilityGameId,
        }, trigger);

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        if (!e.Added || !ReferenceEquals(e.Modifier, AddedRecoveryModifier) || _scheduledEnd is null) return;

        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            _snapshot[spellId] = _spellUsable.CooldownRemaining(spellId, e.Timestamp);
    }

    private void CloseWindow(int timestamp, Event? trigger)
    {
        if (_scheduledEnd is not int scheduledEnd) return;
        var closeAt = Math.Min(timestamp, scheduledEnd);
        _statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, AddedRecoveryModifier, trigger);

        var wallclock = closeAt - _openTimestamp;
        foreach (var (spellId, remainingAtOpen) in _snapshot)
        {
            var onCooldown = Math.Min(wallclock, remainingAtOpen);
            var bonus = (int)(AddedRecovery * onCooldown);
            if (bonus > 0)
                _recoveryBySpell[spellId] = _recoveryBySpell.GetValueOrDefault(spellId) + bonus;
        }

        _snapshot.Clear();
        _scheduledEnd = null;
        _scheduledRemoval = null;
        _windows[^1] = _windows[^1] with { EndTimestamp = closeAt };
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}

/// <summary>A single Glorious Purpose window: the interval over which cooldown recovery was boosted.</summary>
public record GloriousPurposeWindow(int BeginTimestamp, int EndTimestamp);
